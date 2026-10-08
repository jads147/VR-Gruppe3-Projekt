using System;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;

namespace UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets
{
    /// <summary>
    /// Component which reads input values and drives the thumbstick, trigger, and grip transforms
    /// to animate a controller model.
    /// </summary>
    public class Quest3ControllerAnimator : MonoBehaviour
    {
        [Header("Thumbstick")]
        [SerializeField] private Transform thumbstickTransform;
        [SerializeField] private Vector2 thumbstickRotationRange = new Vector2(30f, 30f);
        [SerializeField] private XRInputValueReader<Vector2> thumbstickInput = new XRInputValueReader<Vector2>("Thumbstick");
        private Quaternion initialThumbstickRotation;
        
        [Header("Trigger")]
        [SerializeField] private Transform triggerTransform;
        [SerializeField] private Quaternion pressedTriggerRotation;
        [SerializeField] private XRInputValueReader<float> triggerInput = new XRInputValueReader<float>("Trigger");
        private Quaternion unpressedTriggerRotation;
        
        [Header("Grip")]
        [SerializeField] private Transform gripTransform;
        [SerializeField] private Vector3 pressedGripPosition;
        [SerializeField] private Quaternion pressedGripRotation;
        [SerializeField] private XRInputValueReader<float> gripInput = new XRInputValueReader<float>("Grip");
        private Vector3 unpressedGripPosition;
        private Quaternion unpressedGripRotation;
        
        [Header("Primary Button")]
        [SerializeField] private Transform primaryButtonTransform;
        [SerializeField] private Vector3 pressedPrimaryButtonPosition;
        [SerializeField] private XRInputValueReader<float> primaryButtonInput = new XRInputValueReader<float>("PrimaryButton");
        private Vector3 unpressedPrimaryButtonPosition;
        
        [Header("Secondary Button")]
        [SerializeField] private Transform secondaryButtonTransform;
        [SerializeField] private Vector3 pressedSecondaryButtonPosition;
        [SerializeField] private XRInputValueReader<float> secondaryButtonInput = new XRInputValueReader<float>("SecondaryButton");
        private Vector3 unpressedSecondaryButtonPosition;

        private void Awake()
        {
            unpressedGripPosition = gripTransform.localPosition;
            unpressedGripRotation = gripTransform.localRotation;
            unpressedPrimaryButtonPosition = primaryButtonTransform.localPosition;
            unpressedSecondaryButtonPosition = secondaryButtonTransform.localPosition;
            initialThumbstickRotation = thumbstickTransform.localRotation;
            unpressedTriggerRotation = triggerTransform.localRotation;
        }

        private void OnEnable()
        {
            if (thumbstickTransform == null || gripTransform == null || triggerTransform == null || primaryButtonTransform == null || secondaryButtonTransform == null)
            {
                enabled = false;
                Debug.LogWarning($"Controller Animator component missing references on {gameObject.name}", this);
                return;
            }
            
            thumbstickInput?.EnableDirectActionIfModeUsed();
            triggerInput?.EnableDirectActionIfModeUsed();
            gripInput?.EnableDirectActionIfModeUsed();
            primaryButtonInput?.EnableDirectActionIfModeUsed();
            secondaryButtonInput?.EnableDirectActionIfModeUsed();
        }

        private void OnDisable()
        {
            thumbstickInput?.DisableDirectActionIfModeUsed();
            triggerInput?.DisableDirectActionIfModeUsed();
            gripInput?.DisableDirectActionIfModeUsed();
            primaryButtonInput?.DisableDirectActionIfModeUsed();
            secondaryButtonInput?.DisableDirectActionIfModeUsed();
        }

        private void Update()
        {
            
            if (thumbstickInput != null)
            {
                var stickVal = thumbstickInput.ReadValue();
                thumbstickTransform.localRotation = initialThumbstickRotation * Quaternion.Euler(-stickVal.y * thumbstickRotationRange.x, 0f, -stickVal.x * thumbstickRotationRange.y);
            }

            if (triggerInput != null)
            {
                var triggerVal = triggerInput.ReadValue();
                triggerTransform.localRotation = Quaternion.Lerp(unpressedTriggerRotation, pressedTriggerRotation, triggerVal);
            }
            
            if (gripInput != null)
            {
                var gripVal = gripInput.ReadValue();
                gripTransform.localPosition = Vector3.Lerp(unpressedGripPosition, pressedGripPosition, gripVal);
                gripTransform.localRotation = Quaternion.Lerp(unpressedGripRotation, pressedGripRotation, gripVal);
            }

            if (primaryButtonInput != null)
            {
                var primaryButtonVal = primaryButtonInput.ReadValue();
                primaryButtonTransform.localPosition = Vector3.Lerp(unpressedPrimaryButtonPosition, pressedPrimaryButtonPosition, primaryButtonVal);
            }
            
            if (secondaryButtonInput != null)
            {
                var secondaryButtonVal = secondaryButtonInput.ReadValue();
                secondaryButtonTransform.localPosition = Vector3.Lerp(unpressedSecondaryButtonPosition, pressedSecondaryButtonPosition, secondaryButtonVal);
            }
        }
    }
}