using Duskborn.Audio;
using UnityEngine;

namespace Duskborn.Gameplay.Enemies
{
    /// <summary>Small grounded steps and a raised-cudgel telegraph driven by replicated phases.</summary>
    public sealed class BramblekinPresentation : MonoBehaviour
    {
        [SerializeField] private Bramblekin enemy;
        [SerializeField] private Transform body, head, leftArm, rightArm, leftLeg, rightLeg;
        [SerializeField] private Transform leftShin, rightShin, leftFoot, rightFoot;
        [SerializeField] private AudioClip[] windupClips, swingClips, hurtClips, deathClips, stepClips, hitClips;
        private Transform[] parts;
        private Vector3[] positions;
        private Quaternion[] rotations;
        private Vector3[] rootPositions;
        private Quaternion[] rootRotations;
        private Vector3[] displayedPositions;
        private Quaternion[] displayedRotations;
        private AudioSource action, voice, steps, impact;
        private EnemyRagdoll ragdoll;
        private Vector3 previousPosition;
        private float previousHP, gait, speed, stepAt, hurtAt;
        private int sequence = -1, lastWindup = -1, lastSwing = -1, lastHurt = -1, lastDeath = -1, lastStep = -1, lastHit = -1;
        private bool dead;

        private void Awake()
        {
            parts = new[] { body, head, leftArm, rightArm, leftLeg, rightLeg, leftShin, rightShin, leftFoot, rightFoot };
            positions = new Vector3[parts.Length]; rotations = new Quaternion[parts.Length];
            rootPositions = new Vector3[parts.Length]; rootRotations = new Quaternion[parts.Length];
            displayedPositions = new Vector3[4]; displayedRotations = new Quaternion[4];
            for (int i = 0; i < parts.Length; i++)
            {
                positions[i] = parts[i].localPosition; rotations[i] = parts[i].localRotation;
                rootPositions[i] = transform.InverseTransformPoint(parts[i].position);
                rootRotations[i] = Quaternion.Inverse(transform.rotation) * parts[i].rotation;
            }
            ragdoll = GetComponent<EnemyRagdoll>();
            action = Source(72); voice = Source(80); steps = Source(180); impact = Source(76);
            enemy.OnHealthChanged += HealthChanged; ResetPresentation();
        }
        private AudioSource Source(int priority)
        {
            var source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 1; source.dopplerLevel = 0; source.minDistance = 3; source.maxDistance = 18;
            source.rolloffMode = AudioRolloffMode.Linear; source.priority = priority; return source;
        }
        private void OnDestroy() { if (enemy != null) enemy.OnHealthChanged -= HealthChanged; }
        private void OnDisable() { action?.Stop(); voice?.Stop(); steps?.Stop(); impact?.Stop(); }
        public void ResetPresentation()
        {
            if (parts == null) return;
            action.Stop(); voice.Stop(); steps.Stop(); impact.Stop();
            previousHP = enemy.CurrentHP; previousPosition = transform.position;
            sequence = -1; dead = !enemy.IsAlive; speed = 0; gait = Random.value * Mathf.PI * 2;
            stepAt = Time.time + .3f; hurtAt = -100;
            if (!dead && ragdoll != null && ragdoll.IsRagdoll) ragdoll.DisableRagdoll();
            if (ragdoll == null || !ragdoll.IsRagdoll) Restore();
        }
        private void Restore()
        {
            for (int i = 0; i < parts.Length; i++)
            { parts[i].localPosition = positions[i]; parts[i].localRotation = rotations[i]; }
        }
        private static AudioClip Pick(AudioClip[] clips, ref int last)
        {
            if (clips == null || clips.Length == 0) return null;
            int index = Random.Range(0, clips.Length > 1 && last >= 0 ? clips.Length - 1 : clips.Length);
            if (clips.Length > 1 && last >= 0 && index >= last) index++;
            last = index; return clips[index];
        }
        private static void Play(AudioSource source, AudioClip clip, float gain, float age = 0)
        {
            if (clip == null || age >= clip.length) return;
            source.Stop(); source.clip = clip; source.time = Mathf.Max(0, age);
            source.volume = gain * (AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1);
            source.Play();
        }
        public void PlayClubHit() => Play(impact, Pick(hitClips, ref lastHit), .65f);
        private void HealthChanged(float hp, float maximum)
        {
            float before = previousHP; previousHP = hp;
            if (hp > 0 && before <= 0) { ResetPresentation(); return; }
            if (!enemy.IsClientStarted || !enemy.IsSpawned || hp >= before) return;
            if (hp > 0)
            {
                if (Time.time - hurtAt > .15f) { hurtAt = Time.time; Play(voice, Pick(hurtClips, ref lastHurt), .75f); }
            }
            else if (!dead)
            { dead = true; action.Stop(); steps.Stop(); Play(voice, Pick(deathClips, ref lastDeath), .9f); }
        }
        private void LateUpdate()
        {
            if (!enemy.IsSpawned || !enemy.IsClientStarted) return;
            if (previousHP != enemy.CurrentHP) HealthChanged(enemy.CurrentHP, enemy.MaxHP);
            if (!enemy.IsAlive || (ragdoll != null && ragdoll.IsRagdoll)) return;
            float age = (float)enemy.ActionAge;
            var phase = enemy.View.Phase;
            if (sequence != enemy.View.Sequence)
            {
                sequence = enemy.View.Sequence;
                if (phase == BramblekinPhase.Windup) Play(voice, Pick(windupClips, ref lastWindup), .7f, age);
                else if (phase == BramblekinPhase.Swing) Play(action, Pick(swingClips, ref lastSwing), .85f, age);
            }
            Vector3 travel = transform.position - previousPosition; travel.y = 0;
            previousPosition = transform.position;
            float measured = travel.magnitude / Mathf.Max(Time.deltaTime, .001f);
            // Teleports do not produce a huge step or a footstep burst.
            speed = Mathf.Lerp(speed, measured > 12 ? 0 : measured, Time.deltaTime * 12);
            float stride = Mathf.Clamp01(speed / 6.2f);
            int previousStep = Mathf.FloorToInt(gait / Mathf.PI);
            // Distance-driven scurry: quicker cadence with actual motion, not a fixed idle timer.
            gait += Mathf.Min(speed, 8) * Time.deltaTime * (Mathf.PI * 2 / 1.6f);
            CaptureUpperBody();
            ApplyPose(phase, age, stride, gait);
            SmoothUpperBody(1 - Mathf.Exp(-(phase == BramblekinPhase.Hunt ? 26 : (phase == BramblekinPhase.Swing ? 45 : 26) * BramblekinClock.AttackPlaybackSpeed) * Time.deltaTime));
            if (phase == BramblekinPhase.Hunt && stride > .25f && Mathf.FloorToInt(gait / Mathf.PI) != previousStep && Time.time >= stepAt)
            { stepAt = Time.time + .11f; Play(steps, Pick(stepClips, ref lastStep), .18f); }
        }
        private void CaptureUpperBody()
        {
            for (int i = 0; i < 4; i++)
            {
                displayedPositions[i] = transform.InverseTransformPoint(parts[i].position);
                displayedRotations[i] = Quaternion.Inverse(transform.rotation) * parts[i].rotation;
            }
        }
        private void SmoothUpperBody(float blend)
        {
            for (int i = 0; i < 4; i++)
            {
                parts[i].position = transform.TransformPoint(Vector3.Lerp(displayedPositions[i], transform.InverseTransformPoint(parts[i].position), blend));
                parts[i].rotation = transform.rotation * Quaternion.Slerp(displayedRotations[i], Quaternion.Inverse(transform.rotation) * parts[i].rotation, blend);
            }
        }
        private static float Ease(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(t));
        private void ApplyPose(BramblekinPhase phase, float age, float stride, float stepPhase)
        {
            Restore();
            float wave = Mathf.Sin(stepPhase), delayed = Mathf.Sin(stepPhase - .55f);
            float bounce = Mathf.Cos(stepPhase * 2), headBounce = Mathf.Cos(stepPhase * 2 - .65f);
            Vector3 shift = new Vector3(wave * .025f, -.065f - bounce * .018f, 0) * stride;
            Vector3 torsoAngles = new Vector3(10 + bounce * 2, wave * 9, -wave * 6) * stride;
            Vector3 headAngles = new Vector3(4 + headBounce * 5, -delayed * 5, delayed * 3) * stride;
            Vector3 leftAngles = new Vector3(-delayed * 35, wave * 7, -8 - wave * 6) * stride;
            Vector3 rightAngles = new Vector3(delayed * 30, -wave * 6, 8 - wave * 6) * stride;
            float attack = 0, recoil = 0, follow = 0;
            if (phase == BramblekinPhase.Windup)
            { attack = Ease(age * BramblekinClock.AttackPlaybackSpeed / .20f); recoil = Ease(age * BramblekinClock.AttackPlaybackSpeed / .38f); }
            else if (phase == BramblekinPhase.Swing)
            { attack = 1; follow = Ease(age / BramblekinClock.SwingSeconds); recoil = 1 - follow; }
            else if (phase == BramblekinPhase.Recover)
            { attack = 1 - Ease(age * BramblekinClock.AttackPlaybackSpeed / .38f); follow = 1; }
            // Torso leads the weapon while the neck counters its rotation to keep the gaze readable.
            torsoAngles = Vector3.Lerp(torsoAngles, new Vector3(-8 * recoil + 18 * follow, -15 * recoil + 12 * follow, -5 * recoil + 4 * follow), attack);
            headAngles = Vector3.Lerp(headAngles, new Vector3(-5 * recoil + 9 * follow, 6 * recoil - 5 * follow, 2 * recoil), attack);
            leftAngles = Vector3.Lerp(leftAngles, new Vector3(-28 * recoil + 18 * follow, -10 * recoil, -16 * recoil), attack);
            rightAngles = Vector3.Lerp(rightAngles, new Vector3(-110 * recoil + 30 * follow, 10 * recoil - 12 * follow, -15 * recoil + 8 * follow), attack);
            shift = Vector3.Lerp(shift, new Vector3(0, -.035f - .02f * follow, -.025f * recoil + .035f * follow), attack);
            Quaternion torso = Quaternion.Euler(torsoAngles);
            body.position = transform.TransformPoint(rootPositions[0] + shift);
            body.rotation = transform.rotation * torso * rootRotations[0];
            for (int index = 1; index <= 3; index++)
            {
                Vector3 offset = torso * (rootPositions[index] - rootPositions[0]);
                // Shoulder compression and delayed neck spring share the same stride.
                float shoulder = index == 2 ? -wave : wave;
                offset.y += index == 1 ? (headBounce - bounce) * .008f * stride * (1 - attack) : shoulder * .014f * stride * (1 - attack);
                parts[index].position = transform.TransformPoint(rootPositions[0] + shift + offset);
            }
            head.rotation = transform.rotation * Quaternion.Euler(headAngles) * rootRotations[1];
            leftArm.rotation = transform.rotation * torso * Quaternion.Euler(leftAngles) * rootRotations[2];
            rightArm.rotation = transform.rotation * torso * Quaternion.Euler(rightAngles) * rootRotations[3];
            PoseLeg(4, 6, 8, stepPhase, stride, shift);
            PoseLeg(5, 7, 9, stepPhase + Mathf.PI, stride, shift);
        }
        private void PoseLeg(int hipIndex, int kneeIndex, int footIndex, float phase, float stride, Vector3 shift)
        {
            Vector3 hip = rootPositions[hipIndex] + shift;
            // Smooth lift during swing, flat foot during support. Ankle stays upright.
            float lift = Mathf.Max(0, -Mathf.Sin(phase));
            Vector3 ankle = rootPositions[footIndex] + new Vector3(0, lift * .12f, Mathf.Cos(phase) * .20f) * stride;
            float upper = Vector3.Distance(rootPositions[hipIndex], rootPositions[kneeIndex]);
            float lower = Vector3.Distance(rootPositions[kneeIndex], rootPositions[footIndex]);
            Vector3 direction = ankle - hip;
            float distance = Mathf.Clamp(direction.magnitude, Mathf.Abs(upper - lower) + .001f, upper + lower - .001f);
            direction.Normalize();
            hip = ankle - direction * distance;
            float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            Vector3 bend = Vector3.ProjectOnPlane(Vector3.forward, direction).normalized;
            Vector3 knee = hip + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
            // Keep the intended sole plane even at the limits of the short goblin legs.
            parts[hipIndex].position = transform.TransformPoint(hip);
            parts[hipIndex].rotation = transform.rotation * Quaternion.FromToRotation(rootPositions[kneeIndex] - rootPositions[hipIndex], knee - hip) * rootRotations[hipIndex];
            parts[kneeIndex].position = transform.TransformPoint(knee);
            parts[kneeIndex].rotation = transform.rotation * Quaternion.FromToRotation(rootPositions[footIndex] - rootPositions[kneeIndex], ankle - knee) * rootRotations[kneeIndex];
            parts[footIndex].position = transform.TransformPoint(ankle);
            parts[footIndex].rotation = transform.rotation * rootRotations[footIndex];
        }
    }
}
