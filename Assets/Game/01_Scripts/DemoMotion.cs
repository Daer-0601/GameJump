using UnityEngine;

namespace Gamejam2026
{
    // Transform animation for the simple maintenance suits; no humanoid rig required.
    public class DemoMotion : MonoBehaviour
    {
        public Transform visual;
        public Transform[] legs;
        public Transform[] arms;
        DemoPlayer player;
        Vector3 lastPosition;
        Vector3 handOrigin;
        Vector3 visualOrigin;
        float phase;
        float lastYaw;
        float turn;
        float smoothSpeed;
        float speedVelocity;
        public Animator animator;
        public Transform grip;
        string state = "Locomotion";
        float recoilUntil;
        float interactGrace;
        float armRelease;
        bool armReady;
        Vector3 gripRestPosition;
        Quaternion gripRestRotation;
        Transform upperArm;
        Transform lowerArm;
        Transform fist;
        Transform torso;
        Transform leftLeg;
        Transform rightLeg;
        Transform leftKnee;
        Transform rightKnee;
        Transform leftFoot;
        Transform rightFoot;
        SkinnedMeshRenderer bodyRenderer;
        Transform leftArm;
        Transform rightArm;
        Transform leftElbow;
        Transform rightElbow;
        Transform leftHand;
        float gaitPhase;
        float fearHunch;
        Quaternion carryUpper;
        Quaternion carryLower;
        Quaternion carryFist;
        Quaternion[] armOrigins;
        Vector3[] armScales;
        const float InteractGrace = 0.1f;
        const float CarryArmKeep = 0.9f;

        public void Recoil() { recoilUntil = Time.time + .35f; }

        void Start()
        {
            player = GetComponent<DemoPlayer>();
            if (animator)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            lastPosition = transform.position;
            handOrigin = player.hand.localPosition;
            if (visual) visualOrigin = visual.localPosition;
            lastYaw = transform.eulerAngles.y;
            if (grip)
            {
                gripRestPosition = grip.localPosition;
                gripRestRotation = grip.localRotation;
            }
            CacheCarryBones();
            if (animator && animator.isHuman)
            {
                var candleIK = animator.GetComponent<DemoCandleIK>();
                if (!candleIK) candleIK = animator.gameObject.AddComponent<DemoCandleIK>();
                candleIK.Initialize(this, player, animator);
                leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                leftArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftElbow = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightElbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                bodyRenderer = animator.GetComponentInChildren<SkinnedMeshRenderer>();
            }
            armOrigins = new Quaternion[arms == null ? 0 : arms.Length];
            armScales = new Vector3[armOrigins.Length];
            for (int i = 0; i < armOrigins.Length; i++)
            {
                armOrigins[i] = arms[i] ? arms[i].localRotation : Quaternion.identity;
                armScales[i] = arms[i] ? arms[i].localScale : Vector3.one;
            }
        }

        void CacheCarryBones()
        {
            if (!animator) return;
            if (animator.avatar && animator.avatar.isValid && animator.isHuman)
            {
                upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                fist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                torso = animator.GetBoneTransform(HumanBodyBones.Chest);
                return;
            }
            foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name == "UpperArm.R") upperArm = bone;
                else if (bone.name == "LowerArm.R") lowerArm = bone;
                else if (bone.name == "Fist.R") fist = bone;
                else if (bone.name == "Torso") torso = bone;
            }
        }

        static readonly Vector3 CandleHold = new Vector3(0.2f, 1.0f, 0.42f);

        public Vector3 CandleHandTarget()
        {
            float stride = Mathf.Clamp01(smoothSpeed / Mathf.Max(0.2f, player.moveSpeed));
            float step = Mathf.Sin(Time.time * 6.4f);
            return player.transform.TransformPoint(CandleHold + new Vector3(
                step * 0.012f * stride, Mathf.Abs(step) * 0.016f * stride, 0f));
        }

        void LateUpdate()
        {
            Vector3 displacement = Vector3.ProjectOnPlane(transform.position - lastPosition, Vector3.up);
            lastPosition = transform.position;
            float yaw = Mathf.DeltaAngle(lastYaw, transform.eulerAngles.y);
            lastYaw = transform.eulerAngles.y;
            if (!player || !player.game) return;
            if (!player.game.Playing)
            {
                if (animator && animator.isHuman)
                {
                    smoothSpeed = 0f;
                    ApplyNaturalGait();
                    GroundRig();
                }
                return;
            }
            float speed = player.ActualSpeed;
            float teleportDistance = Mathf.Max(1f, player.moveSpeed * player.runMultiplier * Time.deltaTime * 2f);
            if (displacement.magnitude > teleportDistance)
            {
                speed = 0f;
                speedVelocity = 0f;
            }
            smoothSpeed = Mathf.SmoothDamp(smoothSpeed, speed, ref speedVelocity, 0.2f);
            if (animator && animator.runtimeAnimatorController)
            {
                PresentRig();
                return;
            }
            PresentProcedural(yaw);
        }

        void PresentRig()
        {
            bool carrying = player.Held;
            bool candle = carrying && player.Held.isCandle;
            float clipWalkSpeed = player.moveSpeed * (carrying && !candle ? player.crankSpeedMultiplier : 1f);
            animator.SetFloat("Speed", Mathf.Min(smoothSpeed, clipWalkSpeed));
            animator.SetBool("Crank", carrying && !player.Held.isCandle);
            animator.SetBool("Candle", candle);
            if (player.Working) interactGrace = InteractGrace;
            else interactGrace = Mathf.Max(0f, interactGrace - Time.deltaTime);
            bool interacting = player.Working || interactGrace > 0f;
            bool recoiling = Time.time < recoilUntil;
            string target = recoiling ? "Recoil" : interacting ? "Interact" :
                player.Held ? (player.Held.isCandle ? "Candle" : "Crank") : "Locomotion";
            bool wasHoldingPose = state == "Interact" || state == "Recoil";
            if (target != state)
            {
                bool enterCrouch = target == "Interact" && state != "Interact" && state != "UseEnter" && !carrying;
                float fade = recoiling ? 0.12f : target == "Interact" || state == "Interact" ? 0.42f : 0.36f;
                animator.CrossFadeInFixedTime(enterCrouch ? "UseEnter" : target, fade, 0, 0f);
                state = target;
            }
            else if (state == "Interact" && !carrying && !animator.IsInTransition(0))
            {
                var current = animator.GetCurrentAnimatorStateInfo(0);
                if (current.IsName("UseEnter") && current.normalizedTime >= 0.7f)
                    animator.CrossFadeInFixedTime("Interact", 0.34f, 0, 0f);
            }

            bool holdingPose = state == "Interact" || state == "Recoil";
            if (holdingPose || wasHoldingPose) armRelease = 0.48f;
            else armRelease = Mathf.Max(0f, armRelease - Time.deltaTime);
            if (torso && player.game)
            {
                float fear = player.game.Playing ? player.game.Danger / 100f : 0f;
                fearHunch = Mathf.Lerp(fearHunch, fear * 3f, 1f - Mathf.Exp(-Time.deltaTime * 1.2f));
                torso.localRotation *= Quaternion.Euler(fearHunch, 0f, fearHunch * 0.15f);
            }
            bool protectArm = carrying && !candle && armReady && armRelease > 0f;
            if (carrying && upperArm && lowerArm && fist && !protectArm && !animator.IsInTransition(0))
            {
                float follow = armReady ? 1f - Mathf.Exp(-Time.deltaTime * 8f) : 1f;
                carryUpper = Quaternion.Slerp(carryUpper, upperArm.localRotation, follow);
                carryLower = Quaternion.Slerp(carryLower, lowerArm.localRotation, follow);
                carryFist = Quaternion.Slerp(carryFist, fist.localRotation, follow);
                armReady = true;
            }
            if (protectArm && upperArm && lowerArm && fist)
            {
                upperArm.localRotation = Quaternion.Slerp(upperArm.localRotation, carryUpper, CarryArmKeep);
                lowerArm.localRotation = Quaternion.Slerp(lowerArm.localRotation, carryLower, CarryArmKeep);
                fist.localRotation = Quaternion.Slerp(fist.localRotation, carryFist, CarryArmKeep);
            }
            if (!holdingPose) ApplyNaturalGait(candle, carrying && !candle);
            GroundRig();
            if (candle)
            {
                player.hand.SetPositionAndRotation(CandleHandTarget(), Quaternion.LookRotation(player.transform.forward, Vector3.up));
            }
            else if (grip && carrying)
            {
                grip.localPosition = gripRestPosition;
                grip.localRotation = gripRestRotation;
                player.hand.SetPositionAndRotation(grip.position, grip.rotation);
            }
        }

        // Los clips importados no colocan igual las extremidades de los dos avatares.
        // Esta pose mantiene los pies alternados y los brazos opuestos al caminar.
        void ApplyNaturalGait(bool candle = false, bool crank = false)
        {
            float walk = Mathf.Clamp01(smoothSpeed / Mathf.Max(0.1f, player.moveSpeed));
            float run = Mathf.InverseLerp(player.moveSpeed, player.moveSpeed * player.runMultiplier, smoothSpeed);
            gaitPhase += smoothSpeed * Time.deltaTime * Mathf.Lerp(2.5f, 3.1f, run);
            float step = Mathf.Sin(gaitPhase) * walk * Mathf.Lerp(0.21f, 0.32f, run);
            AimLimb(leftLeg, leftKnee, new Vector3(0f, -1f, step));
            AimLimb(rightLeg, rightKnee, new Vector3(0f, -1f, -step));
            AimLimb(leftKnee, leftFoot, new Vector3(0f, -1f, Mathf.Max(0f, step) * 0.2f));
            AimLimb(rightKnee, rightFoot, new Vector3(0f, -1f, Mathf.Max(0f, -step) * 0.2f));
            AimLimb(leftArm, leftElbow, new Vector3(-0.08f, -1f, -step));
            AimLimb(leftElbow, leftHand, new Vector3(-0.02f, -1f, -step * 0.35f));
            if (!candle && !crank)
            {
                AimLimb(rightArm, rightElbow, new Vector3(0.08f, -1f, step));
                AimLimb(rightElbow, fist, new Vector3(0.02f, -1f, step * 0.35f));
            }
        }

        static void AimLimb(Transform limb, Transform joint, Vector3 localDirection)
        {
            if (!limb || !joint) return;
            Vector3 current = joint.position - limb.position;
            if (current.sqrMagnitude < 0.0001f) return;
            Vector3 desired = limb.root.TransformDirection(localDirection.normalized);
            limb.rotation = Quaternion.FromToRotation(current, desired) * limb.rotation;
        }

        void GroundRig()
        {
            if (!visual || !leftFoot || !rightFoot || !bodyRenderer) return;
            float ground = transform.position.y;
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out var hit, 1.8f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.GetComponentInParent<DemoPlayer>()) ground = hit.point.y;
            visual.position += Vector3.up * (ground - bodyRenderer.bounds.min.y + 0.01f);
        }

        void PresentProcedural(float yaw)
        {
            float blend = 1f - Mathf.Exp(-Time.deltaTime * 8f);
            turn = Mathf.Lerp(turn, Mathf.Clamp(yaw / Mathf.Max(Time.deltaTime, .001f), -160f, 160f), blend);
            float movement = Mathf.Clamp01(smoothSpeed / player.moveSpeed);
            float running = Mathf.InverseLerp(player.moveSpeed, player.moveSpeed * player.runMultiplier, smoothSpeed);
            phase += smoothSpeed * Time.deltaTime * 2.6f;
            float wave = Mathf.Sin(phase);
            if (visual)
            {
                visual.localPosition = visualOrigin + Vector3.up * Mathf.Abs(wave) * Mathf.Lerp(.025f, .045f, running) * movement;
                visual.localRotation = Quaternion.Euler(running * 7f, 0f, wave * Mathf.Lerp(2f, 3f, running) * movement);
            }
            if (legs != null)
                for (int i = 0; i < legs.Length; i++)
                    if (legs[i]) legs[i].localRotation = Quaternion.Euler(wave * (i == 0 ? 1f : -1f) * Mathf.Lerp(22f, 32f, running) * movement, 0f, 0f);
            for (int i = 0; i < armOrigins.Length; i++)
            {
                if (!arms[i]) continue;
                bool carrying = i == 1 && player.Held;
                if (carrying && !player.Stabilizing)
                {
                    Vector3 heldGrip = player.hand.TransformPoint(player.Held.isCandle ? new Vector3(.16f, .11f, 0) : new Vector3(.17f, .17f, 0));
                    Vector3 direction = arms[i].parent.InverseTransformDirection(heldGrip - arms[i].position);
                    arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation, Quaternion.FromToRotation(Vector3.down, direction), blend);
                    float reach = Mathf.Clamp(direction.magnitude / .58f, 1f, 1.5f);
                    arms[i].localScale = Vector3.Lerp(arms[i].localScale, Vector3.Scale(armScales[i], new Vector3(1, reach, 1)), blend);
                    continue;
                }
                float angle = wave * (i == 0 ? -1 : 1) * 18f * movement;
                if (carrying || player.Stabilizing) angle = -38f + angle * .15f;
                arms[i].localRotation = Quaternion.Slerp(arms[i].localRotation, armOrigins[i] * Quaternion.Euler(angle, 0, 0), blend);
                arms[i].localScale = Vector3.Lerp(arms[i].localScale, armScales[i], blend);
            }
            float brace = player.Stabilizing ? .15f : 1f;
            player.hand.localPosition = handOrigin + new Vector3(Mathf.Cos(phase * .5f) * .018f, Mathf.Abs(wave) * Mathf.Lerp(.022f, .035f, running), 0f) * movement * brace;
            player.hand.localRotation = Quaternion.Slerp(player.hand.localRotation,
                Quaternion.Euler((-wave * 3f * movement - running * 3f) * brace, 0, (-wave * 4f * movement - turn * .045f) * brace), blend);
        }
    }
}
