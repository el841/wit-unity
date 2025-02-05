/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// All wit specific settings required by a web socket client in order to connect to a server
    /// </summary>
    public class WitWebSocketSettings
    {
        /// <summary>
        /// The url to connect with on client.Connect()
        /// </summary>
        public string ServerUrl => WitConstants.WIT_SOCKET_URL;

        /// <summary>
        /// The configuration used for wit web socket communication
        /// </summary>
        public IWitRequestConfiguration Configuration { get; }

        /// <summary>
        /// Constructor that takes in configuration
        /// </summary>
        public WitWebSocketSettings(IWitRequestConfiguration configuration)
        {
            Configuration = configuration;
        }
    }
}
