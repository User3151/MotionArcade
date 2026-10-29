using MotionArcade.Core;
using MotionArcade.Feedback;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MotionArcade.Games.BuzzWire
{
    // Buzz Wire: מעבירים טבעת לאורך חוט מפותל בלי לגעת בו. שלושה מסלולים, נבחרים בכפתור TRACK לפני START.
    // המגע מחושב מתמטית (TorusContact) מול נקודות על החוט סביב הנקודה הקרובה לטבעת, בלי קוליידרים:
    // קוליידר על חפץ שמוזז ביד מייצר מגעים קופצניים, וחוט של 8 מ"מ דק מדי בשבילם.
    // ניקוד: אחוז ההתקדמות פחות 3 לכל נגיעה. הסבב נגמר מוקדם כשמגיעים לסוף (עד 120 שניות).
    public class BuzzWireGame : GameStation
    {
        private const string StartHint = "Put the ring around the wire at the BLUE start";
        private const string BackHint = "OFF THE WIRE! Go back to the flashing marker";

        [Header("Tracks")]
        [SerializeField] private WireTrack[] tracks;
        [SerializeField] private float wireRadius = 0.004f;
        [SerializeField] private Button trackButton;
        [SerializeField] private TMP_Text trackButtonLabel;
        [SerializeField] private TMP_Text trackDisplay;
        [SerializeField] private TMP_Text touchesDisplay;
        [SerializeField] private TMP_Text progressDisplay;   // אחוז ההתקדמות, כל הזמן מול העיניים
        // כמה רחוק מעבר לטבעת מותר לחוט "לברוח" לפני שזה נחשב יציאה מהחוט. בטווח הזה זו נגיעה (באזר), לא איפוס.
        [SerializeField] private float offWireTolerance = 0.015f;

        [Header("Wand")]
        [SerializeField] private BuzzWand wand;
        [SerializeField] private Transform wandRest;

        [Header("Feedback")]
        [SerializeField] private AudioSource buzz;   // לופ, על השרביט
        [SerializeField] private AudioSource sfx;
        [SerializeField] private AudioClip checkpointClip;
        [SerializeField] private AudioClip finishClip;
        [SerializeField] private AudioClip offWireClip;

        private readonly WireProgress progress = new WireProgress();
        private int current;
        private float elapsed;
        private double deviationSquares;
        private int deviationSamples;
        private float hapticTimer;
        private bool returnWand;
        private int shownTouches = -1;
        private int shownPercent = -1;
        private bool wasOnWire;

        private WireTrack Track => tracks[current];

        public WireProgress Progress => progress;
        public WireTrack CurrentTrack => Track;

        public override string PlayingHint =>
            Phase != RoundPhase.Playing || progress.OnWire || progress.Finished ? null :
            progress.Checkpoint <= 0f ? StartHint : BackHint;

        public override string ResultDetails
        {
            get
            {
                string text = $"{Track.DisplayName}   Touches {progress.Touches}   Progress {progress.Best * 100f:0}%";
                text += $"\nTime {elapsed:0.0} s";
                if (deviationSamples > 0)
                    text += $"   Average off-centre {System.Math.Sqrt(deviationSquares / deviationSamples) * 1000.0:0.0} mm";
                return text;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            progress.CheckpointReached += HandleCheckpoint;
        }

        private void OnEnable()
        {
            trackButton.onClick.AddListener(NextTrack);
            wand.Released += HandleWandReleased;
        }

        private void OnDisable()
        {
            trackButton.onClick.RemoveListener(NextTrack);
            wand.Released -= HandleWandReleased;
        }

        private void Start()
        {
            ShowTrack(0);
            wand.SetGrabbable(IsRunning);
            ReturnWand();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            progress.CheckpointReached -= HandleCheckpoint;
        }

        protected override void OnPhaseEntered(RoundPhase phase)
        {
            wand.SetGrabbable(IsRunning);
            trackButton.gameObject.SetActive(!IsRunning);   // מחליפים מסלול רק בין סבבים
            if (phase == RoundPhase.Countdown || phase == RoundPhase.Intro)
            {
                progress.Reset();
                ReturnWand();
                Track.ShowMarkers(0f, false, 0f);
                ShowTouches(0);
                ShowProgress(0f);
                wasOnWire = false;
            }
        }

        protected override void OnPlayingStarted()
        {
            progress.Reset();
            elapsed = 0f;
            deviationSquares = 0;
            deviationSamples = 0;
            ShowTouches(0);
        }

        protected override void OnPlayingEnded()
        {
            if (buzz != null) buzz.Stop();
            if (tracks != null && current < tracks.Length && Track != null) Track.SetTouching(false);
        }

        protected override void OnPlayingUpdate() => elapsed += Time.deltaTime;

        // אחרי שה-XRI הזיז את השרביט בפריים הזה (Instantaneous זז ב-Update), כדי לבדוק את המיקום העדכני
        private void LateUpdate()
        {
            if (returnWand)
            {
                returnWand = false;
                ReturnWand();
            }
            if (Phase != RoundPhase.Playing) return;
            Follow();
        }

        private void Follow()
        {
            WireTrack track = Track;
            Transform ring = wand.Ring;
            Vector3 center = ring.position;
            float t = track.Nearest(center, out Vector3 nearest);
            float distance = Vector3.Distance(center, nearest);
            // לא צריך לבדוק שהשרביט ביד: שרביט שלא מוחזק עומד בעמדה שלו, רחוק מכל חוט.
            // החוט מחוץ ללולאה אבל קרוב ("מחליק"): נגיעה, וההתקדמות נעצרת; רק רחוק יותר זו יציאה מהחוט.
            bool slipping = distance >= wand.RingRadius;
            bool threaded = distance < wand.RingRadius + offWireTolerance;

            // מגע: נקודות על החוט סביב הנקודה הקרובה, כל 4 מ"מ, 1.6 ס"מ לכל כיוון
            bool touching = false;
            if (threaded)
            {
                float step = 0.004f / Mathf.Max(0.01f, track.Length);
                for (int k = -4; k <= 4 && !touching; k++)
                {
                    Vector3 local = ring.InverseTransformPoint(track.Evaluate(t + k * step));
                    touching = TorusContact.Touches(local.x, local.y, local.z, wand.RingRadius, wand.RingTube, wireRadius);
                }
            }

            // מחליק: נגיעה, ובלי התקדמות (אחרת אפשר היה להתקדם כשהחוט מחוץ ללולאה)
            if (slipping) touching = true;
            progress.Update(threaded, t, touching, Time.time, advance: !slipping);

            if (wasOnWire && !progress.OnWire && !progress.Finished) HandleOffWire();
            wasOnWire = progress.OnWire;

            if (progress.OnWire)
            {
                deviationSquares += distance * distance;
                deviationSamples++;
            }

            int target = progress.Score;
            if (target != Score) AddScore(target - Score);
            ShowTouches(progress.Touches);
            ShowProgress(progress.Best);
            Feedback(progress.Touching && progress.OnWire);
            track.ShowMarkers(progress.Checkpoint, !progress.OnWire, Time.time);

            if (progress.Finished)
            {
                Feedback(false);
                if (sfx != null && finishClip != null) sfx.PlayOneShot(finishClip);
                FinishRound();
            }
        }

        private void Feedback(bool touching)
        {
            Track.SetTouching(touching);
            if (buzz != null)
            {
                if (touching && !buzz.isPlaying) buzz.Play();
                else if (!touching && buzz.isPlaying) buzz.Stop();
            }
            if (!touching) return;
            hapticTimer -= Time.deltaTime;
            if (hapticTimer > 0f) return;
            hapticTimer = 0.1f;
            Haptics.Pulse(wand.Holder, Haptics.Medium, 0.1f);
        }

        // הטבעת יצאה מהחוט: אסור שזה יעבור בשקט (גיא הגיע לסוף ולא הבין למה לא קרה כלום)
        private void HandleOffWire()
        {
            Feedback(false);
            if (sfx != null && offWireClip != null) sfx.PlayOneShot(offWireClip);
            Haptics.Pulse(wand.Holder, Haptics.Strong, 0.25f);
        }

        private void ShowProgress(float best)
        {
            int percent = (int)(best * 100f + 0.0001f);
            if (percent == shownPercent || progressDisplay == null) return;
            shownPercent = percent;
            progressDisplay.SetText("{0}%", percent);
        }

        private void HandleCheckpoint(float checkpoint)
        {
            if (sfx != null && checkpointClip != null) sfx.PlayOneShot(checkpointClip);
        }

        // XRI עוד מסיים לשחרר באותו פריים; מחזירים את השרביט לעמדה בפריים הבא
        private void HandleWandReleased() => returnWand = true;

        private void ReturnWand()
        {
            if (wandRest != null) wand.Teleport(wandRest.position, wandRest.rotation);
        }

        private void NextTrack()
        {
            if (IsRunning) return;
            ShowTrack((current + 1) % tracks.Length);
        }

        private void ShowTrack(int index)
        {
            current = index;
            for (int i = 0; i < tracks.Length; i++) tracks[i].gameObject.SetActive(i == index);
            string label = Track.DisplayName;
            if (trackButtonLabel != null) trackButtonLabel.text = label + "  >";
            if (trackDisplay != null) trackDisplay.text = label;
            progress.Reset();
            Track.ShowMarkers(0f, false, 0f);
        }

        private void ShowTouches(int touches)
        {
            if (touches == shownTouches || touchesDisplay == null) return;
            shownTouches = touches;
            touchesDisplay.SetText("{0}", touches);
        }
    }
}
