﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.UI.Core;

namespace Microsoft.UnityPlugins
{
    public sealed class Speech
    {
        static SpeechRecognizer speechRecognizer = null;
        static bool isListening = false;
        
        public static void ListenForCommands(IEnumerable<string> commands, Action<CallbackResponse<SpeechArguments>> OnSpeechResults)
        {
            Utils.RunOnWindowsUIThread(async () =>
            {
                try
                {
                    Stop();
                    speechRecognizer = new SpeechRecognizer();
                    speechRecognizer.Constraints.Add(new SpeechRecognitionListConstraint(commands, "Commands"));
                    var compilationResult = await speechRecognizer.CompileConstraintsAsync();
                    if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
                        throw new Exception();

                    DebugLog.Log(LogLevel.Info, "ListenForCommands");

                    speechRecognizer.ContinuousRecognitionSession.Completed += (sender, args) =>
                    {
                        DebugLog.Log(LogLevel.Info, "ListenForCommands " + args.Status.ToString());
                        if (args.Status != SpeechRecognitionResultStatus.Success)
                        {
                            if (OnSpeechResults != null)
                            {
                                Utils.RunOnUnityAppThread(
                                () =>
                                {
                                    OnSpeechResults(new CallbackResponse<SpeechArguments> { Result = new SpeechArguments { Status = SpeechResultStatus.Complete }, Status = CallbackStatus.Success, Exception = null });
                                });
                            }
                            isListening = false;
                        }
                    };

                    speechRecognizer.ContinuousRecognitionSession.ResultGenerated += (sender, args) =>
                    {
                        DebugLog.Log(LogLevel.Info, "ListenForCommands " + args.Result.Text);

                        var command = args.Result.Text;
                        if (!String.IsNullOrEmpty(command))
                            command = command.ToLower();

                        if (commands != null && commands.Count() > 0)
                        {
                            foreach (var c in commands)
                            {
                                if (args != null && args.Result != null && args.Result.Text != null && c.ToLower() == command)
                                {
                                    DebugLog.Log(LogLevel.Info, "ListenForCommands command " + command);
                                    if (OnSpeechResults != null)
                                    {
                                        Utils.RunOnUnityAppThread(
                                        () =>
                                        {
                                            OnSpeechResults(new CallbackResponse<SpeechArguments> { Result = new SpeechArguments { Status = SpeechResultStatus.Command, Text = command }, Status = CallbackStatus.Success, Exception = null });
                                        });
                                    }
                                    break;
                                }
                            }
                        }

                    };

                    await speechRecognizer.ContinuousRecognitionSession.StartAsync();
                    isListening = true;
                }
                catch(Exception ex)
                {
                    DebugLog.Log(LogLevel.Error, "Hit Exception in Speech::ListenForCommands..." + ex.ToString());
                    if (OnSpeechResults != null)
                    {
                        Utils.RunOnUnityAppThread(
                        () =>
                        {
                            OnSpeechResults(new CallbackResponse<SpeechArguments> { Result = null, Status = CallbackStatus.Failure, Exception = ex});
                        });
                    }

                    return;
                }
            });

        }

        public static void ListenForDictation(Action<CallbackResponse<SpeechArguments>> OnSpeechResults)
        {
            Utils.RunOnWindowsUIThread(async () =>
            {
                try
                {
                    Stop();
                    speechRecognizer = new SpeechRecognizer();
                    speechRecognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "Dictation"));
                    var compilationResult = await speechRecognizer.CompileConstraintsAsync();
                    if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
                        throw new Exception();

                    DebugLog.Log(LogLevel.Info, "ListenForDictation");

                    speechRecognizer.HypothesisGenerated += (sender, args) =>
                    {
                        string hypothesis = args.Hypothesis.Text;
                        DebugLog.Log(LogLevel.Info, "ListenForDictation Hypothesis " + hypothesis + "...");
                        if (OnSpeechResults != null)
                        {
                            Utils.RunOnUnityAppThread(
                            () =>
                            {
                                OnSpeechResults(new CallbackResponse<SpeechArguments> { Result = new SpeechArguments { Status = SpeechResultStatus.Hypothesis, Text = hypothesis }, Status = CallbackStatus.Success, Exception = null });
                            });
                        }
                    };

                    speechRecognizer.ContinuousRecognitionSession.Completed += (sender, args) =>
                    {
                        DebugLog.Log(LogLevel.Info, "ListenForDictation " + args.Status.ToString());
                        if (args.Status != SpeechRecognitionResultStatus.Success)
                        {
                            if (OnSpeechResults != null)
                            {
                                Utils.RunOnUnityAppThread(
                                () =>
                                {
                                    OnSpeechResults(new CallbackResponse<SpeechArguments> { Result = new SpeechArguments { Status = SpeechResultStatus.Complete }, Status = CallbackStatus.Success, Exception = null });
                                });
                            }
                            isListening = false;
                        }
                    };


                    speechRecognizer.ContinuousRecognitionSession.ResultGenerated += (sender, args) =>
                    {
                        DebugLog.Log(LogLevel.Info, args.Result.Text);

                        DebugLog.Log(LogLevel.Info, "ListenForDictation " + args.Result.Text);
                        if (OnSpeechResults != null)
                        {
                            Utils.RunOnUnityAppThread(
                            () =>
                            {
                                OnSpeechResults(new CallbackResponse<SpeechArguments> { Result = new SpeechArguments { Status = SpeechResultStatus.Dictation, Text = args.Result.Text }, Status = CallbackStatus.Success, Exception = null });
                            });
                        }
                    };

                    await speechRecognizer.ContinuousRecognitionSession.StartAsync();
                    isListening = true;
                }
                catch(Exception ex)
                {
                    OnSpeechResults(new CallbackResponse<SpeechArguments> { Result = null, Status = CallbackStatus.Failure, Exception = ex });
                    return;
                }
            });

        }

        public static void Stop()
        {
            Utils.RunOnWindowsUIThread(async () =>
            {
                try
                {

                    if (speechRecognizer != null)
                    {
                        try
                        {
                            if (isListening)
                            {
                                await speechRecognizer.ContinuousRecognitionSession.StopAsync();
                                isListening = false;
                            }
                        }
                        catch (Exception)
                        {
                            speechRecognizer.Dispose();
                            speechRecognizer = null;
                            isListening = false;
                        }
                    }
                }
                catch(Exception ex)
                {
                    // If stoping diction crashes, the assumption is that it is ok to ignore it since the scenario is anyways done
                    DebugLog.Log(LogLevel.Error, "Hit an exception in Speech::Stop and ignoring it.." + ex.ToString());
                    return;
                }
            });
        }
       

    }

}
