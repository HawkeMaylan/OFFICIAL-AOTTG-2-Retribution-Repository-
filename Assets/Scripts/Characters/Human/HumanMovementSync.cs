using ApplicationManagers;
using Photon.Pun;
using UnityEngine;
using Utility;

namespace Characters
{
    class HumanMovementSync : BaseMovementSync
    {
        protected Human _human;
        private int? _mountedParentViewID = null;
        private Vector3 _mountedPositionOffset = Vector3.zero;
        private Vector3 _mountedRotationOffset = Vector3.zero;

        protected override void Awake()
        {
            base.Awake();
            _human = GetComponent<Human>();
        }

        protected override void SendCustomStream(PhotonStream stream)
        {
            // Send if mounted (Horse or MapObject)
            bool isMounted = (_human.MountState == HumanMountState.MapObject || _human.MountState == HumanMountState.Horse) && _human.MountedTransform != null;
            stream.SendNext(isMounted);

            if (isMounted)
            {
                PhotonView mountedPV = _human.MountedTransform.GetComponent<PhotonView>();
                if (mountedPV != null)
                {
                    stream.SendNext(mountedPV.ViewID);
                    stream.SendNext(_human.MountedPositionOffset);
                    stream.SendNext(_human.MountedRotationOffset);
                }
                else
                {
                    stream.SendNext(-1); // invalid
                }
            }

            // Send head rotation
            if (_human.LateUpdateHeadRotation.HasValue)
            {
                var rotation = _human.LateUpdateHeadRotation.Value;
                stream.SendNext(QuaternionCompression.CompressQuaternion(ref rotation));
            }
            else
                stream.SendNext(null);
        }

        protected override void ReceiveCustomStream(PhotonStream stream)
        {
            bool isMounted = (bool)stream.ReceiveNext();

            if (isMounted)
            {
                _mountedParentViewID = (int)stream.ReceiveNext();
                _mountedPositionOffset = (Vector3)stream.ReceiveNext();
                _mountedRotationOffset = (Vector3)stream.ReceiveNext();
            }
            else
            {
                _mountedParentViewID = null;
            }

            // Receive head rotation
            int? compressed = (int?)stream.ReceiveNext();
            if (compressed.HasValue)
            {
                var rotation = Quaternion.identity;
                QuaternionCompression.DecompressQuaternion(ref rotation, compressed.Value);
                _human.LateUpdateHeadRotationRecv = rotation;
            }
            else
                _human.LateUpdateHeadRotationRecv = null;
        }

        protected override void Update()
        {
            if (!Disabled && !_photonView.IsMine)
            {
                // Check if mounted by ViewID
                if (_mountedParentViewID.HasValue)
                {
                    PhotonView mountedPV = PhotonView.Find(_mountedParentViewID.Value);
                    if (mountedPV != null)
                    {
                        _transform.position = mountedPV.transform.TransformPoint(_mountedPositionOffset);
                        _transform.rotation = Quaternion.Euler(mountedPV.transform.rotation.eulerAngles + _mountedRotationOffset);
                        return;
                    }
                }

                // Carry syncing
                if (_human.CarryState == HumanCarryState.Carry && _human.Carrier != null)
                {
                    Vector3 offset = _human.Carrier.Cache.Transform.forward * -0.4f + _human.Carrier.Cache.Transform.up * 0.5f;
                    _transform.position = _human.Carrier.Cache.Transform.position + offset;
                    _transform.rotation = _human.Carrier.Cache.Transform.rotation;
                    return;
                }

                // Regular LERP syncing
                _transform.position = Vector3.Lerp(_transform.position, _correctPosition, Time.deltaTime * SmoothingDelay);
                _transform.rotation = Quaternion.Lerp(_transform.rotation, _correctRotation, Time.deltaTime * SmoothingDelay);

                if (_human.BackHuman != null)
                    _human.CarryVelocity = _correctVelocity;

                if (_timeSinceLastMessage < MaxPredictionTime)
                {
                    _correctPosition += _correctVelocity * Time.deltaTime;
                    _timeSinceLastMessage += Time.deltaTime;
                }
            }
        }
    }
}
