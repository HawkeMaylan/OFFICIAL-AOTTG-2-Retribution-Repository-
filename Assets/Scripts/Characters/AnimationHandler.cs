using System;
using UnityEngine;
using ApplicationManagers;
using System.Collections.Generic;

namespace Characters
{
    class AnimationHandler
    {
        private Animation Animation;
        private Animator Animator;
        private SkinnedMeshRenderer Renderer;
        private const float LODBone2Distance = 500f;
        private const float LODBone1Distance = 1000f;
        private Dictionary<string, float> _animationSpeed = new Dictionary<string, float>();
        private string _currentAnimation = string.Empty;
        private float _currentAnimationStartTime = 0f;
        private bool _isLegacy;
        private Dictionary<string, AnimationClip> _animatorClips = new Dictionary<string, AnimationClip>();
        private Dictionary<string, string> _animatorStateNames = new Dictionary<string, string>();

        // New fields for overlay animation system
        private Dictionary<int, AnimationLayer> _animationLayers = new Dictionary<int, AnimationLayer>();
        private int _baseLayer = 0;
        private int _overlayLayer = 1;

        public AnimationHandler(GameObject owner)
        {
            Animation = owner.GetComponent<Animation>();
            Animator = owner.GetComponent<Animator>();

            // Initialize layers
            _animationLayers[_baseLayer] = new AnimationLayer(_baseLayer);
            _animationLayers[_overlayLayer] = new AnimationLayer(_overlayLayer);

            if (Animation != null)
            {
                _isLegacy = true;
                foreach (AnimationState state in Animation)
                {
                    _animationSpeed[state.name] = state.speed;
                    // Set up layers for all animations
                    state.layer = _baseLayer;
                }
            }
            else
            {
                foreach (AnimationClip clip in Animator.runtimeAnimatorController.animationClips)
                {
                    _animatorStateNames[clip.name] = clip.name.Replace('.', '_');
                    _animatorClips[clip.name] = clip;
                    _animationSpeed[clip.name] = 1f;
                }
                Animator.playableGraph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.GameTime);
            }
            Renderer = owner.GetComponentInChildren<SkinnedMeshRenderer>();
        }

        // NEW METHOD: Play overlay animation that doesn't interrupt base animations
        public void PlayOverlay(string name, float fadeTime = 0.1f, float startTime = 0f)
        {
            if (_isLegacy)
            {
                if (!Animation.GetClip(name))
                {
                    Debug.LogWarning($"Animation clip '{name}' not found!");
                    return;
                }

                // Set up the overlay animation
                AnimationState overlayState = Animation[name];
                overlayState.layer = _overlayLayer;
                overlayState.blendMode = AnimationBlendMode.Blend;
                overlayState.weight = 1f;
                overlayState.enabled = true;

                // CrossFade the overlay animation
                Animation.CrossFade(name, fadeTime);
                if (startTime > 0f)
                    Animation[name].normalizedTime = startTime;

                // Store in overlay layer
                _animationLayers[_overlayLayer].SetAnimation(name, Time.time);
            }
            else
            {
                // For Animator, you'd need to set up proper animation layers
                Debug.LogWarning("Overlay animations not implemented for Animator system yet");
                Play(name, startTime);
            }
        }

        // NEW METHOD: Stop overlay animation
        public void StopOverlay(string name, float fadeTime = 0.1f)
        {
            if (_isLegacy)
            {
                if (Animation.IsPlaying(name))
                {
                    Animation.Blend(name, 0f, fadeTime);
                    _animationLayers[_overlayLayer].ClearAnimation();
                }
            }
        }

        // NEW METHOD: Stop all overlay animations
        public void StopAllOverlays(float fadeTime = 0.1f)
        {
            if (_isLegacy)
            {
                foreach (AnimationState state in Animation)
                {
                    if (state.layer == _overlayLayer && Animation.IsPlaying(state.name))
                    {
                        Animation.Blend(state.name, 0f, fadeTime);
                    }
                }
                _animationLayers[_overlayLayer].ClearAnimation();
            }
        }

        // NEW METHOD: Check if overlay is playing
        public bool IsOverlayPlaying(string name)
        {
            if (_isLegacy)
            {
                return Animation.IsPlaying(name) && Animation[name].layer == _overlayLayer;
            }
            return false;
        }

        // NEW METHOD: Get current overlay animation
        public string GetCurrentOverlay()
        {
            if (_isLegacy)
            {
                return _animationLayers[_overlayLayer].CurrentAnimation;
            }
            return string.Empty;
        }

        // MODIFIED: Update base animation tracking to ignore overlay animations
        public string GetCurrentAnimation()
        {
            if (_isLegacy)
            {
                foreach (AnimationState state in Animation)
                {
                    if (Animation.IsPlaying(state.name) && state.layer == _baseLayer)
                        return state.name;
                }
            }
            else
            {
                return _currentAnimation;
            }
            return "";
        }

        // MODIFIED: Play method to ensure base animations stay on base layer
        public void Play(string name, float startTime, bool reset = false)
        {
            if (_isLegacy)
            {
                // Ensure animation is on base layer
                if (Animation.GetClip(name))
                {
                    Animation[name].layer = _baseLayer;
                    Animation[name].weight = 1f;
                    Animation[name].blendMode = AnimationBlendMode.Blend;
                }

                Animation.Play(name);
                if (startTime > 0f || reset)
                    Animation[name].normalizedTime = startTime;
            }
            else
            {
                Animator.Play(_animatorStateNames[name], 0, startTime);
                Animator.speed = GetSpeed(name);
            }
            _currentAnimation = name;
            _currentAnimationStartTime = Time.time;

            // Update layer tracking
            _animationLayers[_baseLayer].SetAnimation(name, Time.time);
        }

        // MODIFIED: CrossFade to ensure base layer
        public void CrossFade(string name, float fade, float startTime)
        {
            if (_isLegacy)
            {
                // Ensure animation is on base layer
                if (Animation.GetClip(name))
                {
                    Animation[name].layer = _baseLayer;
                    Animation[name].weight = 1f;
                    Animation[name].blendMode = AnimationBlendMode.Blend;
                }

                Animation.CrossFade(name, fade);
                if (startTime > 0f)
                    Animation[name].normalizedTime = startTime;
            }
            else
            {
                if (_currentAnimation != string.Empty)
                    fade = fade / GetLength(_currentAnimation);
                Animator.CrossFade(_animatorStateNames[name], fade, 0, startTime);
                Animator.speed = GetSpeed(name);
            }
            _currentAnimation = name;
            _currentAnimationStartTime = Time.time;

            // Update layer tracking
            _animationLayers[_baseLayer].SetAnimation(name, Time.time);
        }

        // NEW METHOD: Set animation layer properties
        public void SetAnimationLayer(string name, int layer, float weight = 1f, AnimationBlendMode blendMode = AnimationBlendMode.Blend)
        {
            if (_isLegacy && Animation.GetClip(name))
            {
                AnimationState state = Animation[name];
                state.layer = layer;
                state.weight = weight;
                state.blendMode = blendMode;
            }
        }

        // Rest of the existing methods remain the same...
        public float GetLength(string name)
        {
            if (_isLegacy)
                return Animation[name].length;
            return _animatorClips[name].length;
        }

        public float GetSpeed(string name)
        {
            return _animationSpeed[name];
        }

        public float GetTotalTime(string name)
        {
            return GetLength(name) / _animationSpeed[name];
        }

        public float GetNormalizedTime(string name)
        {
            if (_isLegacy)
                return Animation[name].normalizedTime;
            throw new Exception("GetNormalizedTime only available for legacy animations.");
        }

        public float GetCurrentNormalizedTime()
        {
            if (_isLegacy)
                return Animation[_currentAnimation].normalizedTime;
            else
            {
                float deltaTime = Time.time - _currentAnimationStartTime;
                float normalizedTime = deltaTime / GetTotalTime(_currentAnimation);
                return normalizedTime;
            }
        }

        public bool IsPlaying(string name)
        {
            if (_isLegacy)
                return Animation.IsPlaying(name);
            return _currentAnimation == name;
        }

        public void SetSpeed(string name, float speed)
        {
            _animationSpeed[name] = speed;
            if (_isLegacy)
                Animation[name].speed = speed;
            else
            {
                if (_currentAnimation == name)
                    Animator.speed = speed;
            }
        }

        public void SetSpeedAll(float speed)
        {
            if (_isLegacy)
            {
                foreach (AnimationState animation in Animation)
                {
                    _animationSpeed[animation.name] = speed;
                    animation.speed = speed;
                }
            }
            else
            {
                foreach (string key in _animationSpeed.Keys)
                {
                    _animationSpeed[key] = speed;
                    if (_currentAnimation == key)
                        Animator.speed = speed;
                }
            }
        }

        public void SetCullingType(bool alwaysAnimate)
        {
            if (_isLegacy)
            {
                if (alwaysAnimate)
                    Animation.cullingType = AnimationCullingType.AlwaysAnimate;
                else
                    Animation.cullingType = AnimationCullingType.BasedOnRenderers;
            }
            else
            {
                if (alwaysAnimate)
                    Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                else
                    Animator.cullingMode = AnimatorCullingMode.CullCompletely;
            }
        }

        public void OnDistanceUpdate(float distance)
        {
            if (distance > LODBone1Distance)
                SetQuality(SkinQuality.Bone1);
            else if (distance > LODBone2Distance)
                SetQuality(SkinQuality.Bone2);
            else
                SetQuality(SkinQuality.Bone4);
            SetShadows(distance < LODBone1Distance);
        }

        private void SetQuality(SkinQuality quality)
        {
            if (Renderer != null && Renderer.quality != quality)
                Renderer.quality = quality;
        }

        private void SetShadows(bool shadows)
        {
            if (Renderer != null && Renderer.receiveShadows != shadows)
            {
                Renderer.receiveShadows = shadows;
                if (shadows)
                    Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                else
                    Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // Helper class to track animation layers
        private class AnimationLayer
        {
            public int LayerIndex { get; private set; }
            public string CurrentAnimation { get; private set; }
            public float StartTime { get; private set; }

            public AnimationLayer(int layerIndex)
            {
                LayerIndex = layerIndex;
                CurrentAnimation = string.Empty;
                StartTime = 0f;
            }

            public void SetAnimation(string animationName, float startTime)
            {
                CurrentAnimation = animationName;
                StartTime = startTime;
            }

            public void ClearAnimation()
            {
                CurrentAnimation = string.Empty;
                StartTime = 0f;
            }
        }
    }
}