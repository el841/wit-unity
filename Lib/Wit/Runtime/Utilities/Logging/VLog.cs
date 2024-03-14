/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.WitAi.Json;
using UnityEditor;
using UnityEngine;

namespace Meta.WitAi
{
    /// <summary>
    /// A class for internal Meta.Voice logs
    /// </summary>
    public static class VLog
    {
        private static ILoggerRegistry _loggerRegistry = LoggerRegistry.Instance;

        #if UNITY_EDITOR
        /// <summary>
        /// If enabled, errors will log to console as warnings
        /// </summary>
        public static bool LogErrorsAsWarnings = false;

        /// <summary>
        /// Ignores logs in editor if less than log level (Error = 0, Warning = 2, Log = 3)
        /// </summary>
        public static VLogLevel EditorLogLevel
        {
            get => _editorLogLevel;
            set
            {
                _editorLogLevel = value;
                UnityEditor.EditorPrefs.SetString(EDITOR_LOG_LEVEL_KEY, _editorLogLevel.ToString());
            }
        }
        private static VLogLevel _editorLogLevel = (VLogLevel)(-1);
        private const string EDITOR_LOG_LEVEL_KEY = "VSDK_EDITOR_LOG_LEVEL";
        private const string EDITOR_FILTER_LOG_KEY = "VSDK_FILTER_LOG";
        private const VLogLevel EDITOR_LOG_LEVEL_DEFAULT = VLogLevel.Warning;

        private static HashSet<string> _filteredTagSet;
        private static List<string> _filteredTagList;

        public static List<String> FilteredTags
        {
            get
            {
                if (null == _filteredTagList)
                {
                    _filteredTagList = new List<string>();
                    var filtered = EditorPrefs.GetString(EDITOR_FILTER_LOG_KEY, null);
                    if (!string.IsNullOrEmpty(filtered))
                    {
                        _filteredTagList = JsonConvert.DeserializeObject<List<string>>(filtered);
                    }
                }

                return _filteredTagList;
            }
        }

        private static HashSet<string> FilteredTagSet
        {
            get
            {
                if (null == _filteredTagSet)
                {
                    _filteredTagSet = new HashSet<string>();
                    foreach (var tag in FilteredTags)
                    {
                        _filteredTagSet.Add(tag);
                    }
                }

                return _filteredTagSet;
            }
        }

        public static void AddTagFilter(string filteredTag)
        {
            if (!FilteredTagSet.Contains(filteredTag))
            {
                _filteredTagList.Add(filteredTag);
                _filteredTagSet.Add(filteredTag);
                SaveFilters();
            }
        }

        public static void RemoveTagFilter(string filteredTag)
        {
            if (FilteredTagSet.Contains(filteredTag))
            {
                _filteredTagList.Remove(filteredTag);
                _filteredTagSet.Remove(filteredTag);
                SaveFilters();
            }
        }

        private static void SaveFilters()
        {
            _filteredTagList.Sort();
            var list = JsonConvert.SerializeObject(_filteredTagList);
            EditorPrefs.SetString(EDITOR_FILTER_LOG_KEY, list);
        }

        // Init on load
        [UnityEngine.RuntimeInitializeOnLoadMethod]
        public static void Init()
        {
            // Already init
            if (_editorLogLevel != (VLogLevel) (-1))
            {
                return;
            }

            // Load log
            string editorLogLevel = UnityEditor.EditorPrefs.GetString(EDITOR_LOG_LEVEL_KEY, EDITOR_LOG_LEVEL_DEFAULT.ToString());

            // Try parsing
            if (!Enum.TryParse(editorLogLevel, out _editorLogLevel))
            {
                // If parsing fails, use default log level
                EditorLogLevel = EDITOR_LOG_LEVEL_DEFAULT;
            }
        }
        #endif

        /// <summary>
        /// Hides all errors from the console
        /// </summary>
        public static bool SuppressLogs { get; set; } = !Application.isEditor && !UnityEngine.Debug.isDebugBuild;

        /// <summary>
        /// Event for appending custom data to a log before logging to console
        /// </summary>
        public static event Action<StringBuilder, string, VLogLevel> OnPreLog;

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level of Info
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void I(object log) => Log(VLogLevel.Info, null, log);
        public static void I(string logCategory, object log) => Log(VLogLevel.Info, logCategory, log);

        /// <summary>
        /// Performs a Debug.Log with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void D(object log) => Log(VLogLevel.Log, null, log);
        public static void D(string logCategory, object log) => Log(VLogLevel.Log, logCategory, log);

        /// <summary>
        /// Performs a Debug.LogWarning with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void W(object log, Exception e = null) => Log(VLogLevel.Warning, null, log, e);
        public static void W(string logCategory, object log, Exception e = null) => Log(VLogLevel.Warning, logCategory, log, e);

        /// <summary>
        /// Performs a Debug.LogError with custom categorization and using the global log level
        /// </summary>
        /// <param name="log">The text to be debugged</param>
        /// <param name="logCategory">The category of the log</param>
        public static void E(object log, Exception e = null) => Log(VLogLevel.Error, null, log, e);
        public static void E(string logCategory, object log, Exception e = null) => Log(VLogLevel.Error, logCategory, log, e);

        /// <summary>
        /// Filters out unwanted logs, appends category information
        /// and performs UnityEngine.Debug.Log as desired
        /// </summary>
        /// <param name="logType"></param>
        /// <param name="log"></param>
        /// <param name="category"></param>
        public static void Log(VLogLevel logType, string logCategory, object log, Exception exception = null)
        {
            #if UNITY_EDITOR
            // Skip logs with higher log type then global log level
            if ((int) logType > (int)EditorLogLevel)
            {
                return;
            }

            if (FilteredTagSet.Contains(logCategory)) return;
            #endif

            // Suppress all except errors
            if (SuppressLogs && (int)logType > (int)VLogLevel.Error)
            {
                return;
            }

            // Use calling category if null
            string category = logCategory;
            if (string.IsNullOrEmpty(category))
            {
                category = GetCallingCategory();
            }

            var logger = _loggerRegistry.GetLogger(category);

            // String builder
            StringBuilder result = new StringBuilder();

            #if !UNITY_EDITOR && !UNITY_ANDROID
            {
                // Start with datetime if not done so automatically
                DateTime now = DateTime.Now;
                result.Append($"[{now.ToShortDateString()} {now.ToShortTimeString()}] ");
            }
            #endif

            // Insert log type
            int start = result.Length;
            result.Append($"[VSDK {logType.ToString().ToUpper()}] ");
            WrapWithLogColor(result, start, logType);

            // Append VDSK & Category
            start = result.Length;
            if (!string.IsNullOrEmpty(category))
            {
                result.Append($"[{category}] ");
            }
            WrapWithCallingLink(result, start);

            // Append the actual log
            result.Append(log == null ? string.Empty : log.ToString());

            // Final log append
            OnPreLog?.Invoke(result, logCategory, logType);

            string message = result.ToString();
            if (null != exception)
            {
                #if UNITY_EDITOR
                message = string.Format("{0}\n<color=\"#ff6666\"><b>{1}:</b> {2}</color>\n=== STACK TRACE ===\n{3}\n=====", result, exception.GetType().Name, exception.Message, FormatStackTrace(exception.StackTrace));
                #endif
            }

            // Log
            switch (logType)
            {

                case VLogLevel.Error:
#if UNITY_EDITOR
                    if (LogErrorsAsWarnings)
                    {
                        logger.Warning(message);
                        return;
                    }
#endif
                    logger.Error(message);
                    break;
                case VLogLevel.Warning:
                    logger.Warning(message);
                    break;
                default:
                    logger.Debug(message);
                    break;
            }
        }

        public static string FormatStackTrace(string stackTrace)
        {
            // Get the project's working directory
            string workingDirectory = Directory.GetCurrentDirectory();
            // Use a regular expression to match lines with a file path and line number
            var regex = new Regex(@"at (.+) in (.*):(\d+)");
            // Use the MatchEvaluator delegate to format the matched lines
            MatchEvaluator evaluator = match =>
            {
                string method = match.Groups[1].Value;
                string filePath = match.Groups[2].Value.Replace(workingDirectory, "");
                string lineNumber = match.Groups[3].Value;
                // Only format the line as a clickable link if the file exists
                if (File.Exists(filePath))
                {
                    string fileName = Path.GetFileName(filePath);
                    return $"at {method} in <a href=\"{filePath}\" line=\"{lineNumber}\">{fileName}:<b>{lineNumber}</b></a>";
                }
                else
                {
                    return match.Value;
                }
            };
            // Replace the matched lines in the stack trace
            string formattedStackTrace = regex.Replace(stackTrace, evaluator);
            return formattedStackTrace;
        }

        /// <summary>
        /// Determines a category from the script name that called the previous method
        /// </summary>
        /// <returns>Assembly name</returns>
        private static string GetCallingCategory()
        {
            // Get stack trace method
            string path = new StackTrace()?.GetFrame(3)?.GetMethod().DeclaringType.Name;
            if (string.IsNullOrEmpty(path))
            {
                return "NoStacktrace";
            }
            // Return path
            return path;
        }

        /// <summary>
        /// Determines a category from the script name that called the previous method
        /// </summary>
        /// <returns>Assembly name</returns>
        private static void WrapWithCallingLink(StringBuilder builder, int startIndex)
        {
            #if UNITY_EDITOR && UNITY_2021_2_OR_NEWER
            StackTrace stackTrace = new StackTrace(true);
            StackFrame stackFrame = stackTrace.GetFrame(3);
            string callingFileName = stackFrame.GetFileName().Replace('\\', '/');
            int callingFileLine = stackFrame.GetFileLineNumber();
            builder.Insert(startIndex, $"<a href=\"{callingFileName}\" line=\"{callingFileLine}\">");
            builder.Append("</a>");
            #endif
        }

        /// <summary>
        /// Get hex value for each log type
        /// </summary>
        private static void WrapWithLogColor(StringBuilder builder, int startIndex, VLogLevel logType)
        {
            #if UNITY_EDITOR
            string hex;
            switch (logType)
            {
                case VLogLevel.Error:
                    hex = "FF0000";
                    break;
                case VLogLevel.Warning:
                    hex = "FFFF00";
                    break;
                default:
                    hex = "00FF00";
                    break;
            }
            builder.Insert(startIndex, $"<color=#{hex}>");
            builder.Append("</color>");
            #endif
        }
    }
}
