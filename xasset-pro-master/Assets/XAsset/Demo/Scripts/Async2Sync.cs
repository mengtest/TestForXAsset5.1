﻿using System.Collections.Generic;
using UnityEngine;

namespace libx {

    // Async2Sync 场景使用
    public class Async2Sync : MonoBehaviour {
        List<AssetRequest> requests = new List<AssetRequest>();

        // Use this for initialization
        void Start() {
            var assetPath = "Assets/XAsset/Demo/UI/Sprites/Async2Sync.png";
            var assetType = typeof(Sprite);
            Assets.updateUnusedAssetsImmediate = true;
            requests.Add(Assets.LoadAssetAsync(assetPath, assetType));
            requests.Add(Assets.LoadAsset(assetPath, assetType));
            Invoke("ReleaseAssets", 2);
        }

        void ReleaseAssets() {
            foreach (var item in requests) {
                item.Release();
            }
            requests.Clear();
            Debug.Log("ReleaseAssets");
        }

        private void Update() {
            if (Input.GetKeyUp(KeyCode.Escape)) {
                Assets.LoadSceneAsync(R.GetScene("Level"));
            }
        }

        void OnDestroy() {
            ReleaseAssets();
            Assets.updateUnusedAssetsImmediate = false;
        }
    }
}
