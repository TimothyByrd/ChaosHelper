using NLog;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace ChaosHelper
{
    //
    // These are the rate limit response headers returned by the site:
    //
    // X-Rate-Limit-Policy: backend-item-request-limit
    // X-Rate-Limit-Rules: Account,Ip
    // X-Rate-Limit-Ip: 45:60:120,180:1800:600
    // X-Rate-Limit-Ip-State: 1:60:0,2:1800:0
    // X-Rate-Limit-Account: 30:60:60,100:1800:600
    // X-Rate-Limit-Account-State: 1:60:0,103:1800:600
    //
    // Explanation:
    //
    // X-Rate-Limit-Policy: backend-item-request-limit
    //    ChaosHelper mostly looks at stash tab pages.
    //    (I think Awakened trade mostly looks at the trade site, so it is concerned with a different limit policy.)
    //
    // X-Rate-Limit-Rules: Account,Ip
    //    There are limits based on the logged in account and based on the originating IP address.
    //    So there are two sets of limit and state headers below.
    //    Since I am getting the contents of stash tabs, I must be logged in and have to follow the stricter Account rules.
    //
    // X-Rate-Limit-Ip: 45:60:120,180:1800:600
    // X-Rate-Limit-Ip-State: 1:60:0,2:1800:0
    //    These are the limits for the originating IP address.
    //    The X-Rate-Limit-Ip contains two rules (separated by a comma)
    //        The first rule 45:60:120 says:
    //            There is a limit of 45 calls in 60 seconds, with a 120-second (2 minute) blackout period if violated.
    //            (During the blackout period calls, will result in a 429 error result.)
    //        The second rule 180:1800:600 says:
    //            There is a limit of 180 calls in 1800 seconds (30 minutes), with a 600-second (10 minute) blackout period if violated.
    //            (During the blackout period calls, will result in a 429 error result.)
    //    The X-Rate-Limit-Ip-State contains two statuses (separated by a comma)
    //        The first status is 1:60:0:
    //            The 60 in the middle means it goes with the 45:60:120 rule above.
    //            This IP adddress has made 1 call in the last 60 seconds.
    //            The final 0 means there are 0 seconds of blackout in effect (i.e. not in violation).
    //        The second status is 2:1800:0:
    //            The 1800 in the middle means it goes with the 180:1800:600 rule above.
    //            This IP adddress has made 2 calls in the last 30 minutes.
    //            The final 0 means there are 0 seconds of blackout in effect (i.e. not in violation).
    //
    // X-Rate-Limit-Account: 30:60:60,100:1800:600
    // X-Rate-Limit-Account-State: 1:60:0,103:1800:600
    //    These are the limits for the logged-in account
    //    The X-Rate-Limit-Account contains two rules (separated by a comma)
    //        The first rule 30:60:60 says:
    //            There is a limit of 30 calls in 60 seconds, with a 60-second blackout period if violated.
    //            (During the blackout period calls, will result in a 429 error result.)
    //        The second rule 180:1000:600 says:
    //            There is a limit of 100 calls in 1800 seconds (30 minutes), with a 600-second (10 minute) blackout period if violated.
    //            (During the blackout period calls, will result in a 429 error result.)
    //    The X-Rate-Limit-Account-State contains two statuses (separated by a comma)
    //        The first status is 1:60:0:
    //            The 60 in the middle means it goes with the 30:60:60 rule above.
    //            This account has made 1 call in the last 60 seconds.
    //            The final 0 means there are 0 seconds of blackout in effect (i.e. not in violation).
    //        The second status is 103:1800:600:
    //            The 1800 in the middle means it goes with the 100:1800:600 rule above.
    //            This account has made 103 calls in the last 30 minutes.
    //            The final 600 means there are 600 seconds of blackout in effect.
    //            That means for the next 10 minutes, further calls will return a 429 error response.
    //
    // I think GGG has picked an awkward set of numbers here. Simply waiting 10 minutes before making
    // another call is not good enough, since we need to wait for calls to drop out of the 30 minute window.
    //

    internal static class RateLimit
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static void UpdateLimits(HttpResponseMessage response)
        {
            if (response == null) return;
            if (!response.Headers.TryGetValues("X-Rate-Limit-Rules", out var ruleTypeValues)
                || ruleTypeValues == null) return;

            if (!response.Headers.TryGetValues("X-Rate-Limit-Policy", out var rulePolicy)
                || rulePolicy == null)
            {
                logger.Warn($"No rate policy");
                return;
            }

            var policy = string.Join(",", rulePolicy);

            // e.g. Account,Ip
            var ruleTypes = string.Join(",", ruleTypeValues).Split(',');
            foreach (var ruleType in ruleTypes)
            {
                UpdateForRuleType(response, ruleType, policy);
            }
        }

        public static async Task DelayForRateLimits(int minimumDelayMS)
        {
            Task minDelayTask = (minimumDelayMS > 0) ? Task.Delay(minimumDelayMS) : null;

            foreach (var ruleKVP in _limitRules) 
            {
                if (ruleKVP.Value.NeedToAwait)
                {
                    logger.Warn($"Doing rate limit on rule {ruleKVP.Key}");
                    await ruleKVP.Value.Await();
                }
            }

            if (minDelayTask != null) await minDelayTask.ConfigureAwait(false);
        }

        private static void UpdateForRuleType(HttpResponseMessage response, string ruleType, string policy)
        {
            //foreach (var header in response.Headers)
            //{
            //    if (header.Key.StartsWith("X-Rate", StringComparison.OrdinalIgnoreCase)
            //        || header.Key.StartsWith("Retry", StringComparison.OrdinalIgnoreCase))
            //    {
            //        var value = string.Join(";", header.Value);
            //        logger.Info($"Header {header.Key}: {value}");
            //    }
            //}

            UpdateRules(response, ruleType, policy);
            UpdateRuleStates(response, ruleType, policy);
        }

        private static void UpdateRules(HttpResponseMessage response, string ruleType, string policy)
        {
            var ruleHeader = $"X-Rate-Limit-{ruleType}";
            if (!response.Headers.TryGetValues(ruleHeader, out var ruleValues)
                || ruleValues == null) return;

            // e.g. 30:60:60,100:1800:600
            var ruleList = string.Join(",", ruleValues).Split(',');

            foreach (var rule in ruleList)
            {
                try
                {
                    var ruleSplit = rule.Split(':');
                    var ruleInts = Array.ConvertAll(ruleSplit, int.Parse);
                    if (ruleInts.Length != 3)
                    {
                        logger.Warn($"Rule '{rule}' in {ruleHeader} is not three values");
                        continue;
                    }
                    UpdateRule(ruleInts[0], ruleInts[1], ruleInts[2], ruleType, policy);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Processing limit rule '{rule}'");
                }
            }
        }

        private static void UpdateRuleStates(HttpResponseMessage response, string ruleType, string policy)
        {
            var ruleStateHeader = $"X-Rate-Limit-{ruleType}-State";
            if (!response.Headers.TryGetValues(ruleStateHeader, out var stateValues)
                || stateValues == null) return;

            // e.g. 1:60:0,103:1800:600
            var stateList = string.Join(",", stateValues).Split(',');

            foreach (var state in stateList)
            {
                try
                {
                    var stateSplit = state.Split(':');
                    var stateInts = Array.ConvertAll(stateSplit, int.Parse);
                    if (stateInts.Length != 3)
                    {
                        logger.Warn($"State '{state}' in {ruleStateHeader} is not three values");
                        continue;
                    }
                    UpdateRuleState(stateInts[0], stateInts[1], stateInts[2], ruleType, policy);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Processing limit rule state '{state}'");
                }
            }
        }

        private static void UpdateRule(int allowedCalls, int windowSeconds, int blackoutSeconds, string ruleType, string policy)
        {
            // e.g. 100, 1800, 600

            var ruleName = LimitRule.MakeName(windowSeconds, ruleType, policy);

            if (!_limitRules.TryGetValue(ruleName, out var rule))
            {
                _limitRules[ruleName] = new LimitRule(allowedCalls, windowSeconds, blackoutSeconds, ruleType, policy);
                return;
            }

            if (rule.AllowedCalls != allowedCalls || rule.BlackoutSeconds != blackoutSeconds)
            {
                rule.AllowedCalls = allowedCalls;
                rule.BlackoutSeconds = blackoutSeconds;
                // need to do anyting else?
            }
        }

        private static void UpdateRuleState(int callsInWindow, int windowSeconds, int activeBlackoutSeconds, string ruleType, string policy)
        {
            // e.g. (1, 60, 0) or (103, 1800, 600)

            var ruleName = LimitRule.MakeName(windowSeconds, ruleType, policy);

            if (!_limitRules.TryGetValue(ruleName, out var rule))
            {
                logger.Warn($"Rule '{ruleName}' not found to update state");
                return;
            }

            rule.UpdateState(callsInWindow, windowSeconds, activeBlackoutSeconds);
        }

        private static Task MakeDelayTask(int delaySeconds) 
        {
            var delayMS = delaySeconds * 1030;
            return Task.Delay(delayMS);
        }

        private static readonly Dictionary<string, LimitRule> _limitRules = [];

        internal class LimitRule(int allowedCalls, int windowSeconds, int blackoutSeconds, string ruleType, string policy)
        {
            public int AllowedCalls { get; set; } = allowedCalls;
            public int WindowSeconds { get; set; } = windowSeconds;
            public int BlackoutSeconds { get; set; } = blackoutSeconds;
            public string RuleType { get; set; } = ruleType.ToLower();
            public string Policy { get; set; } = policy.ToLower();

            public int CallsInWindow { get; set; } = 0;
            public Task WindowDelayTask { get; set; } = null;
            public Task BlackoutTask { get; set; } = null;
            public bool NeedToAwait { get; set; } = false;

            public static string MakeName(int windowSeconds, string ruleType, string policy)
            {
                return $"{windowSeconds}_{ruleType.ToLower()}_{policy.ToLower()}";
            }

            internal void UpdateState(int callsInWindow, int windowSeconds, int activeBlackoutSeconds)
            {
                CallsInWindow = callsInWindow;
                if (CallsInWindow > 0)
                {
                    WindowDelayTask ??= MakeDelayTask(windowSeconds);
                    NeedToAwait = NeedToAwait || CallsInWindow >= AllowedCalls;
                }
                else
                    WindowDelayTask = null;

                if (activeBlackoutSeconds > 0)
                {
                    NeedToAwait = true;
                    BlackoutTask = MakeDelayTask(activeBlackoutSeconds);
                }
            }

            internal async Task Await()
            {
                if (WindowDelayTask != null)
                {
                    await WindowDelayTask.ConfigureAwait(false);
                    WindowDelayTask = null;
                }

                if (BlackoutTask != null)
                {
                    await BlackoutTask.ConfigureAwait(false);
                    BlackoutTask = null;
                }
                NeedToAwait = false;
            }
        }
    }
}
