using UnityEngine;

namespace Gamejam2026
{
    // Vive junto al Animator para que Unity llame a OnAnimatorIK.
    public class DemoCandleIK : MonoBehaviour
    {
        DemoMotion motion;
        DemoPlayer player;
        Animator animator;
        Transform wrist;
        Transform middleFinger;

        public void Initialize(DemoMotion source, DemoPlayer owner, Animator rig)
        {
            motion = source;
            player = owner;
            animator = rig;
            wrist = rig.GetBoneTransform(HumanBodyBones.RightHand);
            middleFinger = rig.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        }

        void OnAnimatorIK(int layerIndex)
        {
            if (!animator || !motion || !player) return;
            bool holding = player.game && player.game.Playing && player.Held &&
                player.Held.isCandle && !player.Held.Inserting;
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, holding ? 1f : 0f);
            animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, holding ? 0.7f : 0f);
            if (!holding) return;
            // El objetivo del IK es la muñeca; el punto que toca la vela es la palma.
            Vector3 palmOffset = wrist && middleFinger
                ? (middleFinger.position - wrist.position) * 0.7f
                : player.transform.forward * 0.07f;
            Vector3 candleGrip = player.Held.transform.position + Vector3.up * 0.085f;
            animator.SetIKPosition(AvatarIKGoal.RightHand, candleGrip - palmOffset);
            animator.SetIKHintPosition(AvatarIKHint.RightElbow,
                player.transform.TransformPoint(new Vector3(0.5f, 1.05f, 0.12f)));
        }
    }
}
