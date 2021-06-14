/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using com.facebook.witai.callbackhandlers;
using com.facebook.witai.lib;
using UnityEditor;
using UnityEngine;

namespace com.facebook.witai.utility
{
    public class WitUnderstandingViewer : BaseWitWindow
    {
        [SerializeField] private Texture2D witHeader;
        private string utterance;
        private bool loading;
        private WitResponseNode response;
        private Dictionary<string, bool> foldouts;

        [NonSerialized]
        private bool initSubmitted;

        [SerializeField]
        private bool submitted;

        private Vector2 scroll;
        private DateTime submitStart;
        private TimeSpan requestLength;
        private string status;

        class Content
        {
            public static GUIContent copyPath;
            public static GUIContent copyCode;

            static Content()
            {
                copyPath = new GUIContent("Copy Path to Clipboard");
                copyCode = new GUIContent("Generate Code on Clipboard");
            }
        }

        [MenuItem("Window/Wit/Understanding Viewer")]
        static void Init()
        {
            WitUnderstandingViewer window = EditorWindow.GetWindow(typeof(WitUnderstandingViewer)) as WitUnderstandingViewer;
            window.titleContent = new GUIContent("Understanding Viewer", WitStyles.WitIcon);
            window.autoRepaintOnSceneChange = true;
            window.Show();
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject)
            {
                var wit = Selection.activeGameObject.GetComponent<Wit>();
                if (wit)
                {
                    wit.events.OnResponse.AddListener((r) =>
                    {
                        response = r;
                        var u = r["text"].Value;
                        if (!string.IsNullOrEmpty(u))
                        {
                            utterance = u;
                        }
                    });
                    status = $"Watching {wit.name} for responses.";
                }
            }
        }

        protected override void OnDrawContent()
        {
            if (!witConfiguration || witConfigs.Length > 1)
            {
                DrawWitConfigurationPopup();

                if (!witConfiguration)
                {
                    GUILayout.Label(
                        "A wit configuration must be available and selected to test utterances.", EditorStyles.helpBox);
                    return;
                }
            }

            GUILayout.BeginHorizontal();
            utterance = EditorGUILayout.TextField("Utterance", utterance);
            if (GUILayout.Button("Submit") && !loading || !initSubmitted && submitted && !string.IsNullOrEmpty(utterance))
            {
                if (!string.IsNullOrEmpty(utterance))
                {
                    submitted = true;
                    initSubmitted = true;
                    SubmitUtterance();
                }
                else
                {
                    loading = false;
                    response = null;
                }
            }
            GUILayout.EndHorizontal();

            if (loading)
            {
                BeginCenter();
                GUILayout.Label("Loading...");
                EndCenter();
            }
            else if (null != response)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
                DrawResponse();
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(
                    "Enter an utterance and hit submit to see what your app will return.");
                GUILayout.EndVertical();
            }
        }

        private void SubmitUtterance()
        {
            if (string.IsNullOrEmpty(utterance))
            {
                loading = false;
                return;
            }

            submitStart = System.DateTime.Now;

            // Hack to watch for loading to complete. Response does not
            // come back on the main thread so Repaint in onResponse in
            // the editor does nothing.
            EditorApplication.update += WatchForResponse;

            var request = witConfiguration.MessageRequest(utterance);
            request.onResponse = r =>
            {
                requestLength = DateTime.Now - submitStart;
                response = r.ResponseData;
                loading = false;
                status = $"Response time: {requestLength}";
            };
            request.Request();
            loading = true;
        }

        private void WatchForResponse()
        {
            if (loading == false)
            {
                Repaint();
                EditorApplication.update -= WatchForResponse;
            }
        }

        private void DrawResponse()
        {
            scroll = GUILayout.BeginScrollView(scroll);
            DrawResponseNode(response);
            GUILayout.EndScrollView();

            GUILayout.FlexibleSpace();
            GUILayout.Label(status, WitStyles.BackgroundBlack25P);
        }

        private void DrawResponseNode(WitResponseNode witResponseNode, string path = "")
        {
            if (null == witResponseNode?.AsObject) return;

            foreach (var child in witResponseNode.AsObject.ChildNodeNames)
            {
                var childNode = witResponseNode[child];
                DrawNode(childNode, child, path);
            }
        }

        private void DrawNode(WitResponseNode childNode, string child, string path, bool isArrayElement = false)
        {
            string childPath;

            if (path.Length > 0)
            {
                childPath = isArrayElement ? $"{path}[{child}]" : $"{path}.{child}";
            }
            else
            {
                childPath = child;
            }

            if (!string.IsNullOrEmpty(childNode.Value))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(15 * EditorGUI.indentLevel);
                if (GUILayout.Button($"{child} = {childNode.Value}", "Label"))
                {
                    ShowNodeMenu(childNode, childPath);
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                var childObject = childNode.AsObject;
                var childArray = childNode.AsArray;

                if ((null != childObject || null != childArray) && Foldout(childPath, child))
                {
                    EditorGUI.indentLevel++;
                    if (null != childObject)
                    {
                        DrawResponseNode(childNode, childPath);
                    }

                    if (null != childArray)
                    {
                        DrawArray(childArray, childPath);
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void ShowNodeMenu(WitResponseNode node, string path)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(Content.copyPath, false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = path;
            });
            menu.AddItem(Content.copyCode, false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = WitResultUtilities.GetCodeFromPath(path);
            });

            if (Selection.activeGameObject)
            {
                menu.AddSeparator("");

                GUIContent label =
                    new GUIContent("Add Value Match Handler to " + Selection.activeObject.name);

                menu.AddItem(label, false, () =>
                {
                    var valueHandler = Selection.activeGameObject.AddComponent<ValueMatchHandler>();
                    valueHandler.intent = response.GetIntentName();
                    valueHandler.valueMatches = new ValueMatch[]
                    {
                        new ValueMatch()
                        {
                            path = path,
                            expectedValue = node.Value
                        }
                    };
                });
                AddValueMatchUpdateItems(path, node.Value, menu);

                menu.AddSeparator("");

                label =
                    new GUIContent("Add Multi Value Handler to " + Selection.activeObject.name);

                menu.AddItem(label, false, () =>
                {
                    var valueHandler = Selection.activeGameObject.AddComponent<MultiValueHandler>();
                    valueHandler.intent = response.GetIntentName();
                    valueHandler.valuePaths = new ValuePathMatcher[]
                    {
                        new ValuePathMatcher() { path = path}
                    };
                });

                AddMultiValueUpdateItems(path, menu);

                menu.AddSeparator("");

                label = new GUIContent("Add Value Handler to " + Selection.activeObject.name);

                menu.AddItem(label, false, () =>
                {
                    var valueHandler = Selection.activeGameObject.AddComponent<ValueHandler>();
                    valueHandler.intent = response.GetIntentName();
                    valueHandler.valuePath = path;
                });
            }

            menu.ShowAsContext();
        }

        private void AddMultiValueUpdateItems(string path, GenericMenu menu)
        {
            var mvhs = Selection.activeGameObject.GetComponents<MultiValueHandler>();
            if (mvhs.Length > 1)
            {
                for (int i = 0; i < mvhs.Length; i++)
                {
                    var handler = mvhs[i];
                    menu.AddItem(
                        new GUIContent("Add Value to Multi Value Handler/Handler " + (i + 1)),
                        false, (h) => AddNewEventHandlerPath((MultiValueHandler) h, path), handler);
                }
            }
            else if (mvhs.Length == 1)
            {
                var handler = mvhs[0];
                menu.AddItem(
                    new GUIContent("Add Value to Multi Value Handler"),
                    false, (h) => AddNewEventHandlerPath((MultiValueHandler) h, path), handler);
            }
        }

        private void AddValueMatchUpdateItems(string path, string value, GenericMenu menu)
        {
            var mvhs = Selection.activeGameObject.GetComponents<ValueMatchHandler>();
            if (mvhs.Length > 1)
            {
                for (int i = 0; i < mvhs.Length; i++)
                {
                    var handler = mvhs[i];
                    menu.AddItem(
                        new GUIContent("Add Match to Value Match Handler/Handler " + (i + 1)),
                        false, (h) => AddNewValueMatchHandlerPath((ValueMatchHandler) h, path, value), handler);
                }
            }
            else if (mvhs.Length == 1)
            {
                var handler = mvhs[0];
                menu.AddItem(
                    new GUIContent("Add Match to Value Match Handler"),
                    false, (h) => AddNewValueMatchHandlerPath((ValueMatchHandler) h, path, value), handler);
            }
        }

        private void AddNewValueMatchHandlerPath(ValueMatchHandler handler, string path, string value)
        {
            Array.Resize(ref handler.valueMatches, handler.valueMatches.Length + 1);
            handler.valueMatches[handler.valueMatches.Length - 1] = new ValueMatch()
            {
                path = path,
                expectedValue = value
            };
        }

        private void AddNewEventHandlerPath(MultiValueHandler handler, string path)
        {
            Array.Resize(ref handler.valuePaths, handler.valuePaths.Length + 1);
            handler.valuePaths[handler.valuePaths.Length - 1] = new ValuePathMatcher()
            {
                path = path
            };
        }

        private void DrawArray(WitResponseArray childArray, string childPath)
        {
            for (int i = 0; i < childArray.Count; i++)
            {
                DrawNode(childArray[i], i.ToString(), childPath, true);
            }
        }

        private bool Foldout(string path, string label)
        {
            if (null == foldouts) foldouts = new Dictionary<string, bool>();
            if (!foldouts.TryGetValue(path, out var state))
            {
                state = false;
                foldouts[path] = state;
            }

            var newState = EditorGUILayout.Foldout(state, label);
            if (newState != state)
            {
                foldouts[path] = newState;
            }

            return newState;
        }
    }
}
