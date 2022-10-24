/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi;
using Meta.WitAi.Events;
using UnityEngine.Events;

namespace Facebook.WitAi.Windows
{
    public class WitUnderstandingViewerVoiceServiceAPI : WitUnderstandingViewerServiceAPI
    {
        private VoiceService _service;

        public WitUnderstandingViewerVoiceServiceAPI(VoiceService service) : base(service)
        {
            _service = service;

            HasVoiceActivation = true;
            HasTextActivation = true;
        }

        public override bool Active
        {
            get => _service.Active;
        }

        public override bool MicActive
        {
            get => _service.MicActive;
        }

        public override bool IsRequestActive
        {
            get => _service.IsRequestActive;
        }

        public override void Activate()
        {
            _service.Activate();
        }

        public override void Activate(string text)
        {
            _service.Activate(text);
        }

        public override void Deactivate()
        {
            _service.Deactivate();
        }

        public override void DeactivateAndAbortRequest()
        {
            _service.DeactivateAndAbortRequest();
        }

        public override WitRequestCreatedEvent OnRequestCreated
        {
            get => _service.VoiceEvents.OnRequestCreated;
        }

        public override WitErrorEvent OnError
        {
            get => _service.VoiceEvents.OnError;
        }

        public override WitResponseEvent OnResponse
        {
            get => _service.VoiceEvents.OnResponse;
        }

        public override WitTranscriptionEvent OnFullTranscription
        {
            get => _service.VoiceEvents.onFullTranscription;
        }

        public override WitTranscriptionEvent OnPartialTranscription
        {
            get => _service.VoiceEvents.OnPartialTranscription;
        }

        public override UnityEvent OnStoppedListening
        {
            get => _service.VoiceEvents.OnStoppedListening;
        }
    }
}
