using ApplicationManagers;
using GameManagers;
using Photon.Pun;
using Settings;
using UnityEngine;
using Utility;

namespace Effects
{
    class EffectSpawner
    {
        public static void Spawn(string name, Vector3 position, Quaternion rotation, float scale = 1f, bool scaleSize = true, object[] settings = null)
        {
            RPCManager.PhotonView.RPC("SpawnEffectRPC", RpcTarget.All, new object[] { name, position, rotation, scale, scaleSize, settings });
        }

        public static void OnSpawnEffectRPC(string name, Vector3 position, Quaternion rotation, float scale, bool scaleSize, object[] settings, PhotonMessageInfo info)
        {
            GameObject go;
            go = ResourceManager.InstantiateAsset<GameObject>(ResourcePaths.Effects, name, position, rotation);
            BaseEffect effect;
            if (name == EffectPrefabs.ThunderspearExplode)
            {
                effect = go.AddComponent<ThunderspearExplodeEffect>();
                effect.Setup(info.Sender, 10f, settings);
            }
            else if (name == EffectPrefabs.APGTrail)
            {
                effect = go.AddComponent<LineRendererEffect>();
                effect.Setup(info.Sender, 10f, settings);
            }
            else if (name == EffectPrefabs.GasBurst)
            {
                effect = go.AddComponent<GasBurstEffect>();
                effect.Setup(info.Sender, 10f, settings);
            }
            else if (name == EffectPrefabs.Blood2)
            {
                float lifeTime = 10000000000f;
            }
            else if (name == EffectPrefabs.TitanDie1)
            {
                float lifeTime = 60f;
            }
            else if (name == EffectPrefabs.TitanDie2)
            {
                float lifeTime = 60f;
            }

            else
            {
                float lifeTime = 10f;

                if (settings != null)
                    if (settings[0] is float)
                        lifeTime = (float)settings[0];

                effect = go.AddComponent<BaseEffect>();
                effect.Setup(info.Sender, lifeTime, settings);
                effect.Setup(info.Sender, 10f, settings);
            }
            ScaleEffect(go.transform, scale, scaleSize);
        }

        private static void ScaleEffect(Transform transform, float scale, bool scaleSize)
        {
            transform.localScale = new Vector3(scale, scale, scale);
            if (!scaleSize)
                return;
            foreach (ParticleSystem system in transform.GetComponentsInChildren<ParticleSystem>())
            {
                var emission = system.main;
                emission.startSpeedMultiplier *= scale;
                emission.startSizeMultiplier *= scale;
            }
        }
    }
}
