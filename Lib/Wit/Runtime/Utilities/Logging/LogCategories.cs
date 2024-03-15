﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Logging
{
    /// <summary>
    /// The core log categories used by the VSDK.
    /// This is not an exhaustive list, since additional categories can be specified by name.
    /// </summary>
    public enum LogCategories
    {
        Global,
        Conduit,
        ManifestGenerator,
    }
}
