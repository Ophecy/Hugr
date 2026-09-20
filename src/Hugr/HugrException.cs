// Copyright (C) 2026 Ophecy
// SPDX-License-Identifier: GPL-3.0-or-later

using System;

namespace Hugr
{
    /// <summary>
    /// Raised when the game no longer exposes what Hugr needs. Never swallowed silently: caught
    /// at the patch boundary and logged with its code, which names the failing step.
    /// </summary>
    internal class HugrException : Exception
    {
        internal HugrException(string code, string message)
            : base(code + ": " + message)
        {
        }
    }
}
