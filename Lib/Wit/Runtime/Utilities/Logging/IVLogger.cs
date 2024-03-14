﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi;

namespace Lib.Wit.Runtime.Utilities.Logging
{
    /// <summary>
    /// The VSDK Logger. Each class should have its own instance of the logger.
    /// Instances should be created via <see cref="ILoggerRegistry"/>.
    /// </summary>
    public interface IVLogger
    {
        /// <summary>
        /// The correlation ID allows the tracing of an operation from beginning to end.
        /// It can be linked to other IDs to form a full chain when it branches out or moves to other domains.
        /// If not supplied explicitly while logging, it will be inherited from the thread storage or a
        /// new one will be generated if none exist.
        /// </summary>
        CorrelationID CorrelationID { get; set; }

        /// <summary>
        /// Logs a verbose message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Verbose(string message, params object [] parameters);

        /// <summary>
        /// Logs a verbose message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Verbose(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Info(string message, params object [] parameters);

        /// <summary>
        /// Logs an info message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Info(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Debug(string message, params object [] parameters);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Debug(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Warning(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Warning(string message, params object [] parameters);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="correlationId">The correlation ID.</param>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Error(CorrelationID correlationId, string message, params object [] parameters);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message as a format string (e.g "My value is: {0}).</param>
        /// <param name="parameters">The parameters.</param>
        void Error(string message, params object [] parameters);

        /// <summary>
        /// Returns a logging scope to be used in a "using" block.
        /// </summary>
        /// <param name="verbosity">The verbosity of the logging.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="parameters">The parameter</param>
        /// <returns>The scope.</returns>
        public LogScope Scope(VLogLevel verbosity, string message, params object[] parameters)
        {
            return new LogScope(this, verbosity, CorrelationID, message, parameters);
        }

        /// <summary>
        /// Returns a logging scope to be used in a "using" block.
        /// </summary>
        /// <param name="verbosity">The verbosity of the logging.</param>
        /// <param name="correlationId">The correlation ID to use for the scope.</param>
        /// <param name="message">The message to log.</param>
        /// <param name="parameters">The parameter</param>
        /// <returns>The scope.</returns>
        public LogScope Scope(VLogLevel verbosity, CorrelationID correlationId, string message, params object[] parameters)
        {
            return new LogScope(this, verbosity, correlationId, message, parameters);
        }

        /// <summary>
        /// Explicitly start a scope.
        /// </summary>
        /// <param name="verbosity"></param>
        /// <param name="correlationId"></param>
        /// <param name="message"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public int Start(VLogLevel verbosity, CorrelationID correlationId, string message, params object[] parameters);

        /// <summary>
        /// Explicitly start a scope.
        /// </summary>
        /// <param name="verbosity"></param>
        /// <param name="message"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public int Start(VLogLevel verbosity, string message, params object[] parameters);

        /// <summary>
        /// Explicitly end a scope. Must have been started already.
        /// </summary>
        /// <param name="sequenceId"></param>
        void End(int sequenceId);

        /// <summary>
        /// Writes out any high verbosity logs that have been suppressed.
        /// </summary>
        void Flush();
    }
}
