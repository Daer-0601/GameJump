using Unity.Collections;
using UnityEngine;

namespace Gamejam2026
{
    // Traduce el estado del anfitrión a la copia visual del cliente. No decide reglas.
    public static class LanPresentation
    {
        static ushort lastSequence;
        static bool hasSequence;
        static int lastContacts = -1;

        public static void ResetSequence()
        {
            hasSequence = false;
            lastSequence = 0;
            lastContacts = -1;
        }

        public static LanSnapshot Capture(DemoGame game, ushort sequence)
        {
            var snap = new LanSnapshot
            {
                version = 1,
                sequence = sequence,
                focusMech = 255,
                focusStation = 255
            };
            if (!game) return snap;
            game.CopyReplicaClock(out snap.elapsed, out snap.danger, out snap.candle, out snap.containment, out snap.containing, out snap.disturbance, out int seals, out int contacts);
            snap.completed = (byte)Mathf.Clamp(seals, 0, 255);
            snap.contacts = (byte)Mathf.Clamp(contacts, 0, 255);
            snap.paused = game.PausedReplica;
            snap.finished = game.Finished;
            snap.won = game.WonReplica;
            snap.defeat = Clip128(game.DefeatCause);
            if (game.Investigation)
            {
                game.Investigation.CopyEvidence(out snap.evidence, out snap.contrasted, out snap.revealed);
                game.Investigation.CopyAnomalies(out int altered, out snap.bodyMoved);
                snap.altered = (sbyte)Mathf.Clamp(altered, -1, 120);
                game.Investigation.CopyShadow(out Vector3 shadow, out float yaw, out snap.shadowMoving, out snap.shadowOn);
                snap.sx = shadow.x;
                snap.sy = shadow.y;
                snap.sz = shadow.z;
                snap.syaw = yaw;
                var stations = game.Investigation.Stations;
                for (int i = 0; i < stations.Count && i < 6; i++)
                {
                    if (!stations[i]) continue;
                    stations[i].CaptureReplica(out float progress, out float seconds, out byte flags);
                    snap.SetStation(i, new LanStationNet { progress = progress, secondsLeft = seconds, flags = flags });
                }
                var growths = game.Investigation.Growths;
                int count = 0;
                for (int i = 0; i < growths.Count && count < LanSnapshot.MaxGrowth; i++)
                {
                    var growth = growths[i];
                    if (!growth) continue;
                    snap.SetGrowth(count, new LanGrowthNet
                    {
                        id = (ushort)Mathf.Clamp(growth.ReplicaId, 0, ushort.MaxValue),
                        burn = growth.Burn01 * 2f,
                        consumed = growth.Consumed
                    });
                    count++;
                }
                snap.growthCount = (byte)count;
            }
            if (game.mechanisms != null)
            {
                for (int i = 0; i < game.mechanisms.Length && i < 3; i++)
                {
                    var mechanism = game.mechanisms[i];
                    if (!mechanism) continue;
                    snap.SetMech(i, new LanMech
                    {
                        done = mechanism.done,
                        unstable = mechanism.unstable,
                        cooperation = mechanism.Cooperation01 * DemoMechanism.CooperationSeconds,
                        hold = mechanism.Hold01 * 1.5f
                    });
                }
            }
            WritePose(game, 0, ref snap.p0);
            WritePose(game, 1, ref snap.p1);
            CarryObject candle = game.candles != null && game.candles.Length > 0 ? game.candles[0] : null;
            CarryObject crank = game.crank ? game.crank : FindCrank();
            WriteCarry(candle, game.mechanisms, out snap.candleAnchor, out snap.cax, out snap.cay, out snap.caz, out snap.cayaw);
            WriteCarry(crank, game.mechanisms, out snap.crankAnchor, out snap.crx, out snap.cry, out snap.crz, out snap.cryaw);
            if (game.players != null && game.players.Length > 1 && game.players[1])
            {
                var remote = game.players[1];
                snap.hint = Clip256(remote.Hint);
                snap.working = remote.Working;
                snap.stabilizing = remote.Stabilizing != null;
                snap.needsHand = remote.NeedsFreeHand;
                snap.focusMech = IndexOf(game.mechanisms, remote.FocusedMechanism);
                snap.focusStation = StationIndex(game, remote.FocusedStation);
            }
            return snap;
        }

        public static void Apply(DemoGame game, LanSnapshot snap)
        {
            if (!game || snap.version != 1 || !LanAuthority.IsClientView) return;
            if (hasSequence && (ushort)(snap.sequence - lastSequence) >= 32768) return;
            hasSequence = true;
            lastSequence = snap.sequence;
            if (game.mechanisms != null)
            {
                for (int i = 0; i < game.mechanisms.Length && i < 3; i++)
                    if (game.mechanisms[i]) game.mechanisms[i].ApplyReplica(snap.MechAt(i).done, snap.MechAt(i).unstable, snap.MechAt(i).cooperation, snap.MechAt(i).hold);
            }
            CarryObject candle = game.candles != null && game.candles.Length > 0 ? game.candles[0] : null;
            CarryObject crank = game.crank ? game.crank : FindCrank();
            if (candle) candle.ApplyAnchor(snap.candleAnchor, new Vector3(snap.cax, snap.cay, snap.caz), snap.cayaw, game.players, game.mechanisms);
            if (crank) crank.ApplyAnchor(snap.crankAnchor, new Vector3(snap.crx, snap.cry, snap.crz), snap.cryaw, game.players, game.mechanisms);
            if (game.Investigation)
            {
                game.Investigation.ApplyEvidence(snap.evidence, snap.contrasted, snap.revealed);
                var stations = game.Investigation.Stations;
                for (int i = 0; i < stations.Count && i < 6; i++)
                {
                    if (!stations[i]) continue;
                    var station = snap.StationAt(i);
                    stations[i].ApplyReplica(station.progress, station.secondsLeft, station.flags);
                }
                ApplyGrowths(game, snap);
                game.Investigation.ApplyShadow(new Vector3(snap.sx, snap.sy, snap.sz), snap.syaw, snap.shadowMoving, snap.shadowOn);
                game.Investigation.ApplyAnomalies(snap.altered, snap.bodyMoved);
            }
            ApplyPose(game, 0, snap.p0);
            ApplyPose(game, 1, snap.p1);
            if (game.players != null && game.players.Length > 1 && game.players[1])
            {
                DemoMechanism mechanism = snap.focusMech < 255 && game.mechanisms != null && snap.focusMech < game.mechanisms.Length ? game.mechanisms[snap.focusMech] : null;
                RecintoStation station = null;
                if (snap.focusStation < 255 && game.Investigation != null && snap.focusStation < game.Investigation.Stations.Count)
                    station = game.Investigation.Stations[snap.focusStation];
                game.players[1].SetReplicaFocus(snap.hint.ToString(), mechanism, station, snap.working, snap.stabilizing, snap.needsHand);
            }
            int previousContacts = lastContacts;
            game.ApplyReplicaClock(snap.elapsed, snap.danger, snap.candle, snap.containment, snap.containing, snap.disturbance, snap.completed, snap.contacts);
            if (previousContacts >= 0 && snap.contacts > previousContacts)
                game.PresentContact();
            lastContacts = snap.contacts;
            game.ApplyReplicaPause(snap.paused);
            if (snap.finished) game.ApplyReplicaFinish(snap.won, snap.defeat.ToString());
        }

        static void ApplyGrowths(DemoGame game, LanSnapshot snap)
        {
            var growths = game.Investigation.Growths;
            int count = Mathf.Min(snap.growthCount, LanSnapshot.MaxGrowth);
            for (int i = 0; i < count; i++)
            {
                LanGrowthNet sample = snap.GrowthAt(i);
                RecintoContamination match = null;
                for (int g = 0; g < growths.Count; g++)
                {
                    if (growths[g] && growths[g].ReplicaId == sample.id) { match = growths[g]; break; }
                }
                if (match) match.ApplyReplica(sample.burn, sample.consumed);
            }
        }

        static void WritePose(DemoGame game, int index, ref LanPose pose)
        {
            if (game.players == null || index >= game.players.Length || !game.players[index]) return;
            var player = game.players[index];
            pose.x = player.transform.position.x;
            pose.y = player.transform.position.y;
            pose.z = player.transform.position.z;
            pose.yaw = player.transform.eulerAngles.y;
            pose.pitch = player.Pitch;
        }

        static void ApplyPose(DemoGame game, int index, LanPose pose)
        {
            if (game.players == null || index >= game.players.Length || !game.players[index]) return;
            bool local = index == LanAuthority.LocalSlot;
            game.players[index].SetReplicaPose(new Vector3(pose.x, pose.y, pose.z), pose.yaw, pose.pitch, local);
        }

        static void WriteCarry(CarryObject item, DemoMechanism[] mechanisms, out byte anchor, out float x, out float y, out float z, out float yaw)
        {
            anchor = 0;
            x = y = z = yaw = 0f;
            if (!item) return;
            anchor = item.CaptureAnchor(mechanisms, out Vector3 pos, out yaw);
            x = pos.x;
            y = pos.y;
            z = pos.z;
        }

        static CarryObject FindCrank()
        {
            var items = Object.FindObjectsByType<CarryObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < items.Length; i++)
                if (items[i] && !items[i].isCandle) return items[i];
            return null;
        }

        static byte IndexOf(DemoMechanism[] mechanisms, DemoMechanism mechanism)
        {
            if (mechanisms == null || !mechanism) return 255;
            for (int i = 0; i < mechanisms.Length && i < 255; i++)
                if (mechanisms[i] == mechanism) return (byte)i;
            return 255;
        }

        static byte StationIndex(DemoGame game, RecintoStation station)
        {
            if (!game.Investigation || !station) return 255;
            var stations = game.Investigation.Stations;
            for (int i = 0; i < stations.Count && i < 255; i++)
                if (stations[i] == station) return (byte)i;
            return 255;
        }

        static FixedString128Bytes Clip128(string value)
        {
            var text = new FixedString128Bytes();
            AppendLimited(ref text, value, 120);
            return text;
        }

        static FixedString512Bytes Clip256(string value)
        {
            var text = new FixedString512Bytes();
            AppendLimited(ref text, value, 240);
            return text;
        }

        static void AppendLimited<T>(ref T text, string value, int maxChars) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            if (string.IsNullOrEmpty(value)) return;
            int count = Mathf.Min(value.Length, maxChars);
            for (int i = 0; i < count; i++)
                if (text.Append(value[i]) != FormatError.None) break;
        }
    }
}
