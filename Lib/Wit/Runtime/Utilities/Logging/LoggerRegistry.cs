﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Meta.Voice.Logging
{
    public sealed class LoggerRegistry : ILoggerRegistry
    {
        private Dictionary<string, IVLogger> _loggers = new Dictionary<string, IVLogger>();

        /// <summary>
        /// The singleton instance of the registry.
        /// </summary>
        public static ILoggerRegistry Instance { get; } = new LoggerRegistry();

        /// <summary>
        /// A private constructor to prevent instantiation of this class.
        /// </summary>
        private LoggerRegistry()
        {
        }

        /// <inheritdoc/>
        public IVLogger GetLogger()
        {
            var stackTrace = new StackTrace();
            var category = LogCategory.Global.ToString();

            var callingFrame = stackTrace.GetFrames()?.Skip(1).FirstOrDefault(frame => frame?.GetMethod()?.DeclaringType != typeof(LoggerRegistry));
            var callerType = callingFrame?.GetMethod()?.DeclaringType;

            if (callerType == null)
            {
                return GetLogger(category);
            }

            var attribute = callerType.GetCustomAttribute<LogCategoryAttribute>();
            if (attribute == null)
            {
                return new VLogger(category);
            }

            category = attribute.CategoryName;

            return GetLogger(category);
        }

        /// <inheritdoc/>
        public IVLogger GetLogger(string category)
        {
            if (!_loggers.ContainsKey(category))
            {
                _loggers.Add(category, new VLogger(category));
            }

            return _loggers[category];
        }
    }
}
