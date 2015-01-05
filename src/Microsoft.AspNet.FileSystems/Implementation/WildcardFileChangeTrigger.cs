// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Text.RegularExpressions;

namespace Microsoft.AspNet.FileSystems
{
    internal class WildcardFileChangeTrigger : FileExpirationTriggerBase
    {
        private Regex _searchRegex;

        public WildcardFileChangeTrigger(string pattern)
        {
            Pattern = pattern;
        }

        public string Pattern { get; }

        private Regex SearchRegex
        {
            get
            {
                if (_searchRegex == null)
                {
                    // Perf: Compile this as this may be used multiple times.
                    _searchRegex = new Regex('^' + Pattern + '$', RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
                }

                return _searchRegex;
            }
        }

        public bool IsMatch(string relativePath)
        {
            return SearchRegex.IsMatch(relativePath);
        }
    }
}