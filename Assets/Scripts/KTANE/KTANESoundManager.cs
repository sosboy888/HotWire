// KTANESoundManager.cs
// Procedural audio for the KTANE bomb — no audio files required.
// All clips are synthesised at runtime using AudioClip.SetData with
// waveform mathematics (oscillator approach).
//
// SINGLETON USAGE:
//   KTANESoundManager.Instance?.PlayTick();
//   KTANESoundManager.Instance?.PlayCorrect();
//   KTANESoundManager.Instance?.PlayWrong();
//   KTANESoundManager.Instance?.PlaySolve();
//   KTANESoundManager.Instance?.PlayPageTurn();
//   KTANESoundManager.Instance?.PlayReady();
//
// SCENE SETUP:
//   BuildKTANEScene creates this automatically.
//   It needs no Inspector references; it builds its own AudioSource.

using System.Collections;
using UnityEngine;

namespace KTANE
{
    public class KTANESoundManager : MonoBehaviour
    {
        // ----- Singleton ------------------------------------------------
        public static KTANESoundManager Instance { get; private set; }

        // ----- Inspector ------------------------------------------------
        [Header("Volume (0–1)")]
        [Range(0f, 1f)] public float masterVolume = 0.70f;

        // ----- Private --------------------------------------------------
        private AudioSource _source;
        private int         _rate;   // output sample rate

        // Pre-built clips
        private AudioClip   _tickClip;
        private AudioClip   _correctClip;
        private AudioClip   _wrongClip;
        private AudioClip   _pageTurnClip;
        private AudioClip   _readyClip;
        private AudioClip[] _solveClips;   // C-major arpeggio (4 notes)

        // ================================================================
        // Unity lifecycle
        // ================================================================

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _rate   = AudioSettings.outputSampleRate;
            _source = gameObject.AddComponent<AudioSource>();
            _source.spatialBlend = 0f;   // 2-D (global)
            _source.playOnAwake  = false;

            BuildClips();
        }

        // ================================================================
        // Public API
        // ================================================================

        /// <summary>Short mechanical click — called every second by the timer.</summary>
        public void PlayTick()     => _source.PlayOneShot(_tickClip,     masterVolume);

        /// <summary>Rising beep — correct interaction with a module element.</summary>
        public void PlayCorrect()  => _source.PlayOneShot(_correctClip,  masterVolume);

        /// <summary>Low buzz — wrong input / strike.</summary>
        public void PlayWrong()    => _source.PlayOneShot(_wrongClip,    masterVolume);

        /// <summary>C-major arpeggio — module fully solved.</summary>
        public void PlaySolve()    => StartCoroutine(ArpeggioRoutine());

        /// <summary>Subtle high click — manual page turned.</summary>
        public void PlayPageTurn() => _source.PlayOneShot(_pageTurnClip, masterVolume);

        /// <summary>Warm confirmation tone — game about to start.</summary>
        public void PlayReady()    => _source.PlayOneShot(_readyClip,    masterVolume);

        // ================================================================
        // Clip construction
        // ================================================================

        private void BuildClips()
        {
            // Tick  — 1 800 Hz sine, 30 ms, sharp linear decay
            _tickClip     = Tone(1800f, 0.030f, 0.28f, WaveForm.Sine);

            // Correct — 880 Hz sine, 80 ms
            _correctClip  = Tone(880f,  0.080f, 0.25f, WaveForm.Sine);

            // Wrong — 120 Hz square, 320 ms
            _wrongClip    = Tone(120f,  0.320f, 0.40f, WaveForm.Square);

            // Page turn — 1 400 Hz sine, 40 ms, quiet
            _pageTurnClip = Tone(1400f, 0.040f, 0.15f, WaveForm.Sine);

            // Ready — 660 Hz sine, 220 ms, warm
            _readyClip    = Tone(660f,  0.220f, 0.35f, WaveForm.Sine);

            // Solve arpeggio — C5 E5 G5 C6
            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f };
            _solveClips = new AudioClip[freqs.Length];
            for (int i = 0; i < freqs.Length; i++)
                _solveClips[i] = Tone(freqs[i], 0.140f, 0.32f, WaveForm.Sine);
        }

        private enum WaveForm { Sine, Square }

        /// <summary>
        /// Synthesise a mono clip with a linear-decay amplitude envelope.
        /// </summary>
        private AudioClip Tone(float freq, float duration, float peak, WaveForm form)
        {
            int    n   = Mathf.Max(1, (int)(_rate * duration));
            float[] d  = new float[n];
            float   w  = 2f * Mathf.PI * freq;

            for (int i = 0; i < n; i++)
            {
                float t   = (float)i / _rate;
                float env = 1f - (float)i / n;   // linear decay 1→0
                float s   = form == WaveForm.Square
                    ? (Mathf.Sin(w * t) >= 0f ? 1f : -1f)
                    : Mathf.Sin(w * t);
                d[i] = s * env * peak;
            }

            var clip = AudioClip.Create("sfx", n, 1, _rate, false);
            clip.SetData(d, 0);
            return clip;
        }

        // ================================================================
        // Arpeggio coroutine
        // ================================================================

        private IEnumerator ArpeggioRoutine()
        {
            foreach (var clip in _solveClips)
            {
                _source.PlayOneShot(clip, masterVolume);
                yield return new WaitForSeconds(0.10f);
            }
        }
    }
}
