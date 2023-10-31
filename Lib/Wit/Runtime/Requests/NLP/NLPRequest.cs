/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text;
using Meta.WitAi;
using UnityEngine.Events;

namespace Meta.Voice
{
    /// <summary>
    /// Abstract class for NLP text & audio requests
    /// </summary>
    /// <typeparam name="TUnityEvent">The type of event callback performed by TEvents for all event callbacks</typeparam>
    /// <typeparam name="TOptions">The type containing all specific options to be passed to the end service.</typeparam>
    /// <typeparam name="TEvents">The type containing all events of TSession to be called throughout the lifecycle of the request.</typeparam>
    /// <typeparam name="TResults">The type containing all data that can be returned from the end service.</typeparam>
    public abstract class NLPRequest<TUnityEvent, TOptions, TEvents, TResults, TResponseData>
        : TranscriptionRequest<TUnityEvent, TOptions, TEvents, TResults>
        where TUnityEvent : UnityEventBase
        where TOptions : INLPRequestOptions
        where TEvents : NLPRequestEvents<TUnityEvent, TResponseData>
        where TResults : INLPRequestResults<TResponseData>
    {
        /// <summary>
        /// Getter for request input type
        /// </summary>
        public NLPRequestInputType InputType => Options == null ? NLPRequestInputType.Audio : Options.InputType;

        /// <summary>
        /// Getter for decoded response data
        /// </summary>
        public TResponseData ResponseData => Results == null ? default(TResponseData) : Results.ResponseData;

        // Ensure initialized only once
        private bool _initialized = false;
        // Ensure final is not called multiple times
        private bool _finalized = false;

        /// <summary>
        /// Constructor for NLP requests
        /// </summary>
        /// <param name="newInputType">The input type for nlp request transmission</param>
        /// <param name="newOptions">The request parameters sent to the backend service</param>
        /// <param name="newEvents">The request events to be called throughout it's lifecycle</param>
        protected NLPRequest(NLPRequestInputType inputType, TOptions options, TEvents newEvents) : base(options, newEvents)
        {
            // Set option input type & bools
            var opt = Options;
            opt.InputType = inputType;
            Options = opt;
            _initialized = true;
            _finalized = false;

            // Finalize
            SetState(VoiceRequestState.Initialized);
        }

        /// <summary>
        /// Sets the NLPRequest object to the given state, but only after being initialized
        /// </summary>
        protected override void SetState(VoiceRequestState newState)
        {
            if (_initialized)
            {
                base.SetState(newState);
            }
        }

        /// <summary>
        /// Append NLP request specific data to log
        /// </summary>
        /// <param name="log">Building log</param>
        /// <param name="warning">True if this is a warning log</param>
        protected override void AppendLogData(StringBuilder log, VLogLevel logLevel)
        {
            base.AppendLogData(log, logLevel);
            log.AppendLine($"Input Type: {InputType}");
        }

        /// <summary>
        /// Throw error on text request
        /// </summary>
        protected override string GetActivateAudioError()
        {
            if (InputType == NLPRequestInputType.Text)
            {
                return "Cannot activate audio on a text request";
            }
            return string.Empty;
        }

        /// <summary>
        /// Throw error on text request
        /// </summary>
        protected override string GetSendError()
        {
            if (InputType == NLPRequestInputType.Audio && !IsAudioInputActivated)
            {
                return "Cannot send audio without activation";
            }
            return base.GetSendError();
        }

        /// <summary>
        /// Getter for status code from response data
        /// </summary>
        protected abstract int GetResponseStatusCode(TResponseData responseData);

        /// <summary>
        /// Getter for error from response data if applicable
        /// </summary>
        protected abstract string GetResponseError(TResponseData responseData);

        /// <summary>
        /// Getter for whether response data contains partial (early) response data
        /// </summary>
        protected abstract bool GetResponseHasPartial(TResponseData responseData);

        /// <summary>
        /// Sets response data to the current results object
        /// </summary>
        /// <param name="responseData">Parsed json data returned from request</param>
        /// <param name="final">Whether or not this response should be considered final</param>
        protected virtual void ApplyResponseData(TResponseData responseData, bool final)
        {
            // Ignore if not active
            if (!IsActive)
            {
                return;
            }
            // Only perform final once
            if (final)
            {
                if (_finalized)
                {
                    return;
                }
                _finalized = true;
            }
            // Handle null response
            if (responseData == null)
            {
                if (final)
                {
                    HandleFailure($"Failed to decode partial raw response");
                }
                return;
            }
            // Handle error
            string error = GetResponseError(responseData);
            if (!string.IsNullOrEmpty(error))
            {
                if (final)
                {
                    HandleFailure(GetResponseStatusCode(responseData), error);
                }
                return;
            }

            // Store whether data is changing
            bool hasChanged = !responseData.Equals(Results.ResponseData);

            // Apply new response data
            Results.SetResponseData(responseData);

            // Call partial response if changed & exists
            bool hasPartial = GetResponseHasPartial(responseData);
            if ((hasChanged && hasPartial) || (final && !hasPartial))
            {
                OnPartialResponse();
            }

            // Final was called, handle success
            if (final)
            {
                OnFullResponse();
                HandleSuccess();
            }
        }

        /// <summary>
        /// Called when response data has been updated
        /// </summary>
        protected virtual void OnPartialResponse() =>
            Events?.OnPartialResponse?.Invoke(ResponseData);

        /// <summary>
        /// Called when full response has completed
        /// </summary>
        protected virtual void OnFullResponse() =>
            Events?.OnFullResponse?.Invoke(ResponseData);

        /// <summary>
        /// Cancels the current request but handles success immediately if possible
        /// </summary>
        public virtual void CompleteEarly()
        {
            // Ignore if not in correct state
            if (!IsActive || _finalized)
            {
                return;
            }

            // Cancel instead
            if (ResponseData == null)
            {
                Cancel("Cannot complete early without response data");
            }
            // Handle success
            else
            {
                MakeLastResponseFinal();
            }
        }

        // Make current response final if possible
        protected virtual void MakeLastResponseFinal()
        {
            // Ignore if not active
            if (!IsActive)
            {
                return;
            }

            // Return previous data
            ApplyResponseData(ResponseData, true);
        }
    }
}
