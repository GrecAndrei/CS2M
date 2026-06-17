// SPDX-License-Identifier: MIT
// Token shape validation shared by the REST surface and the UDP
// dispatcher. Strict here means "doesn't crash on bad input" not
// "is a known valid token". Auth happens in M4.

using System.Text.RegularExpressions;

namespace CS2M.ApiServer.Core.Validation;

public static class TokenValidator
{
    // 1..128 chars, ASCII letters/digits, dot, dash, underscore.
    private static readonly Regex Shape = new(@"^[A-Za-z0-9._-]{1,128}$", RegexOptions.Compiled);

    public static bool IsWellFormed(string? token, out string error)
    {
        if (string.IsNullOrEmpty(token))
        {
            error = "token is required";
            return false;
        }
        if (token.Length > 128)
        {
            error = "token exceeds 128 characters";
            return false;
        }
        if (!Shape.IsMatch(token))
        {
            error = "token contains invalid characters; allowed: A-Z a-z 0-9 . _ -";
            return false;
        }
        error = string.Empty;
        return true;
    }
}