using System;
using UnityEngine;

namespace libx {
    public class NetworkMonitor : MonoBehaviour {
        // ����״�������ı��Ļص�
        public Action<NetworkReachability> onReachabilityChanged;

        private static NetworkMonitor instance;

        // ����
        public static NetworkMonitor Instance {
            get {
                if (instance == null) {
                    instance = new GameObject("NetworkMonitor").AddComponent<NetworkMonitor>();
                    DontDestroyOnLoad(instance.gameObject);
                }
                return instance;
            }
        }

        public NetworkReachability reachability { get; private set; }

        // ����ʱ��
        public float sampleTime = 0.5f;

        // �����ϴγ���������󾭹���ʱ�� ��
        private float _timeSinceLevelLoad;

        // �Ƿ���ͣ��
        private bool _paused;

        private void Start() {
            reachability = Application.internetReachability;
            UnPause();
        }

        // ȡ����ͣ
        public void UnPause() {
            _timeSinceLevelLoad = Time.timeSinceLevelLoad;
            _paused = false;
        }

        public void Pause() {
            _paused = true;
        }

        private void Update() {
            if (_paused) {
                return;
            }

            if (!(Time.timeSinceLevelLoad - _timeSinceLevelLoad >= sampleTime))
                return;

            NetworkReachability networkReachability = Application.internetReachability;

            if (reachability != networkReachability) {
                if (onReachabilityChanged != null) {
                    // ����״�������˸ı�
                    onReachabilityChanged(networkReachability);
                }
                reachability = networkReachability;
            }

            _timeSinceLevelLoad = Time.timeSinceLevelLoad;
        }
    }
}