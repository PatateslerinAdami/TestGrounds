using System;
using System.Collections.Generic;

namespace LeagueSandbox.GameServer.GameObjects.StatsNS
{
    /// <summary>
    /// Riot's "Karma" pseudo-random roller - verbatim port of S4 StatsKarma.cpp
    /// (Karma::RollKarma). Streak-compensated crit/dodge rolls: a per-stream fail
    /// counter (1..5, +1 per miss, reset on success) multiplies a per-chance table
    /// constant; long streaks of bad luck become increasingly unlikely. Streams keep
    /// independent counters (S4 AIHero: 0 = crit vs enemy heroes, 2 = crit vs
    /// minions/monsters; 1/3 were dodge). Replay-proven live in 4.x: post-crit
    /// suppression ratio 0.68 and a rising P(crit|miss-streak) curve — see memory
    /// "crit-karma-prd".
    ///
    /// The constants are RIOT'S ACTUAL VALUES, extracted from the 4.20 client's
    /// DATA/Globals/Critical.inibin (section [Karma], keys Critical0..200 — the S1
    /// source shows InitKarmaConstants loading exactly that file; the inibin is
    /// byte-identical from S1-era dumps through 4.20). Keys Critical0 and Critical200
    /// are absent from the file; Riot's loader defaulted them to -1.0. In practice
    /// only bucket 0 matters (chance in (0.0001%, 0.49%] never crits): bucket 200's
    /// window [99.99%, 99.9999%) is unreachable because the >= 0.999999 early-out in
    /// Roll already returns true WITHOUT rolling — at 100% crit chance there is no
    /// roll and no counter touch, it always crits. Replicated verbatim. Sanity: an independent numerical re-derivation (Markov solve of the
    /// long-run-rate==chance constraint) reproduces these values within ~2%,
    /// confirming the table is rate-preserving by construction.
    /// </summary>
    public class KarmaRandom
    {
        // mKarmaConstants — index = (int)((chance + 0.0001) / 0.005), 201 buckets.
        private static readonly float[] KarmaConstants =
        {
            -1.000000f, 0.001020f, 0.002050f, 0.003100f, 0.004180f, 0.005270f,
            0.006390f, 0.007530f, 0.008700f, 0.009890f, 0.011100f, 0.012340f,
            0.013610f, 0.014900f, 0.016230f, 0.017580f, 0.018960f, 0.020370f,
            0.021810f, 0.023280f, 0.024790f, 0.026320f, 0.027900f, 0.029500f,
            0.031140f, 0.032820f, 0.034540f, 0.036290f, 0.038080f, 0.039920f,
            0.041790f, 0.043710f, 0.045660f, 0.047660f, 0.049710f, 0.051800f,
            0.053940f, 0.056120f, 0.058350f, 0.060630f, 0.062960f, 0.065340f,
            0.067770f, 0.070250f, 0.072780f, 0.075370f, 0.078010f, 0.080700f,
            0.083450f, 0.086260f, 0.089120f, 0.092040f, 0.095020f, 0.098050f,
            0.101140f, 0.104290f, 0.107500f, 0.110760f, 0.114080f, 0.117470f,
            0.120910f, 0.124410f, 0.127960f, 0.131580f, 0.135250f, 0.138980f,
            0.142770f, 0.146620f, 0.150520f, 0.154480f, 0.158490f, 0.162560f,
            0.166680f, 0.171130f, 0.175590f, 0.180060f, 0.184530f, 0.189020f,
            0.193520f, 0.198030f, 0.202560f, 0.207110f, 0.211830f, 0.217000f,
            0.222140f, 0.227260f, 0.232370f, 0.237460f, 0.242540f, 0.247610f,
            0.252670f, 0.257720f, 0.262770f, 0.267810f, 0.272850f, 0.277900f,
            0.282940f, 0.288680f, 0.295220f, 0.301710f, 0.308140f, 0.314520f,
            0.320840f, 0.327120f, 0.333340f, 0.339520f, 0.345650f, 0.351740f,
            0.357780f, 0.363780f, 0.369730f, 0.375650f, 0.381530f, 0.387380f,
            0.393180f, 0.398960f, 0.404700f, 0.410400f, 0.416080f, 0.421720f,
            0.427340f, 0.432930f, 0.438490f, 0.444030f, 0.454030f, 0.464300f,
            0.474440f, 0.484470f, 0.494390f, 0.504200f, 0.513900f, 0.523490f,
            0.532980f, 0.542360f, 0.551640f, 0.560820f, 0.569900f, 0.578890f,
            0.587780f, 0.596570f, 0.605270f, 0.613890f, 0.622410f, 0.630840f,
            0.639190f, 0.647450f, 0.655620f, 0.663720f, 0.671730f, 0.679660f,
            0.687510f, 0.695280f, 0.702980f, 0.710600f, 0.718150f, 0.725620f,
            0.733020f, 0.740350f, 0.747610f, 0.754790f, 0.761910f, 0.768970f,
            0.775950f, 0.782870f, 0.789730f, 0.796520f, 0.803250f, 0.809920f,
            0.816520f, 0.823070f, 0.829560f, 0.835980f, 0.842350f, 0.848660f,
            0.854920f, 0.861120f, 0.867270f, 0.873360f, 0.879400f, 0.885380f,
            0.891310f, 0.897200f, 0.903030f, 0.908810f, 0.914540f, 0.920220f,
            0.925860f, 0.931440f, 0.936980f, 0.942480f, 0.947930f, 0.953330f,
            0.958690f, 0.964000f, 0.969270f, 0.974500f, 0.979680f, 0.984830f,
            0.989930f, 0.994990f, -1.000000f,
        };

        private static readonly Random _random = new Random();

        // mKarmaValues / mKarmaDecay — per-stream fail counter and last-seen chance.
        private readonly Dictionary<int, float> _values = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _decay = new Dictionary<int, float>();

        private static float LookupKarma(float chance)
        {
            int index = (int)(chance / 0.005f);
            return KarmaConstants[Math.Clamp(index, 0, KarmaConstants.Length - 1)];
        }

        /// <summary>
        /// S4 Karma::RollKarma(float, int) — including the rescale branch that converts
        /// the accumulated counter when the nominal chance INCREASES mid-game (item
        /// buy): the old effective chance is preserved by re-expressing it as an
        /// equivalent counter under the new chance's table constant.
        /// </summary>
        public bool Roll(float chance, int id)
        {
            const float minChance = 0.000001f;
            if (chance <= minChance)
            {
                return false;
            }
            if (chance >= 0.999999f)
            {
                return true;
            }

            if (!_values.ContainsKey(id))
            {
                _values[id] = 1.0f;
                _decay[id] = chance;
            }

            float oldCritChance = chance + 0.0001f;
            float modCritChance = LookupKarma(oldCritChance);
            float multiplier = Math.Min(1.0f - (1.0f - oldCritChance) / 5.0f,
                _values[id] * modCritChance);

            if (oldCritChance > _decay[id])
            {
                float decayChance = _decay[id];
                float decayLookup = LookupKarma(decayChance);
                modCritChance = 1.0f - (1.0f - decayChance) / 5.0f;
                modCritChance = Math.Min(modCritChance, _values[id] * decayLookup);

                float rescaled = modCritChance / LookupKarma(oldCritChance);
                if (rescaled > 1.0f)
                {
                    _values[id] = rescaled;
                }
                else
                {
                    _values[id] = 1.0f;
                    modCritChance = multiplier;
                }
            }
            else
            {
                modCritChance = multiplier;
            }

            _decay[id] = oldCritChance;

            if (modCritChance > (float)_random.NextDouble())
            {
                _values[id] = 1.0f;
                return true;
            }

            _values[id] += 1.0f;
            if (_values[id] >= 5.0f)
            {
                _values[id] = 5.0f;
            }

            return false;
        }
    }
}
