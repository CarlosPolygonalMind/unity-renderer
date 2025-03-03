using System;
using AvatarSystem;
using DCL;
using DCL.Components;
using DCL.Helpers;
using UnityEngine;

public class AvatarAnimatorLegacy : MonoBehaviour, IPoolLifecycleHandler, IAnimator
{
    const float IDLE_TRANSITION_TIME = 0.2f;
    const float STRAFE_TRANSITION_TIME = 0.25f;
    const float RUN_TRANSITION_TIME = 0.15f;
    const float WALK_TRANSITION_TIME = 0.15f;
    const float JUMP_TRANSITION_TIME = 0.01f;
    const float FALL_TRANSITION_TIME = 0.5f;
    const float EXPRESSION_TRANSITION_TIME = 0.2f;

    const float AIR_EXIT_TRANSITION_TIME = 0.2f;
    const float GROUND_BLENDTREE_TRANSITION_TIME = 0.15f;

    const float RUN_SPEED_THRESHOLD = 0.05f;
    const float WALK_SPEED_THRESHOLD = 0.03f;

    const float ELEVATION_OFFSET = 0.6f;
    const float RAY_OFFSET_LENGTH = 3.0f;

    const float MAX_VELOCITY = 6.25f;

    [System.Serializable]
    public class AvatarLocomotion
    {
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;
        public AnimationClip jump;
        public AnimationClip fall;
    }

    [System.Serializable]
    public class BlackBoard
    {
        public float walkSpeedFactor;
        public float runSpeedFactor;
        public float movementSpeed;
        public float verticalSpeed;
        public bool isGrounded;
        public string expressionTriggerId;
        public long expressionTriggerTimestamp;
        public float deltaTime;
    }

    [SerializeField] internal AvatarLocomotion femaleLocomotions;
    [SerializeField] internal AvatarLocomotion maleLocomotions;
    AvatarLocomotion currentLocomotions;

    public new Animation animation;
    public BlackBoard blackboard;
    public Transform target;

    [SerializeField] float runMinSpeed = 6f;
    [SerializeField] float walkMinSpeed = 0.1f;

    internal System.Action<BlackBoard> currentState;

    Vector3 lastPosition;
    bool isOwnPlayer = false;
    private AvatarAnimationEventHandler animEventHandler;

    public void Start() { OnPoolGet(); }

    public void OnPoolGet()
    {
        if (DCLCharacterController.i != null)
        {
            isOwnPlayer = DCLCharacterController.i.transform == transform.parent;

            // NOTE: disable MonoBehaviour's update to use DCLCharacterController event instead
            this.enabled = !isOwnPlayer;

            if (isOwnPlayer)
            {
                DCLCharacterController.i.OnUpdateFinish += Update;
            }
        }

        currentState = State_Init;
    }

    public void OnPoolRelease()
    {
        if (isOwnPlayer && DCLCharacterController.i)
        {
            DCLCharacterController.i.OnUpdateFinish -= Update;
        }
    }

    void Update() { Update(Time.deltaTime); }

    void Update(float deltaTime)
    {
        if (target == null || animation == null)
            return;

        blackboard.deltaTime = deltaTime;
        UpdateInterface();
        currentState?.Invoke(blackboard);
    }

    void UpdateInterface()
    {
        Vector3 velocityTargetPosition = target.position;
        Vector3 flattenedVelocity = velocityTargetPosition - lastPosition;

        //NOTE(Brian): Vertical speed
        float verticalVelocity = flattenedVelocity.y;
        blackboard.verticalSpeed = verticalVelocity;

        flattenedVelocity.y = 0;

        if (isOwnPlayer)
            blackboard.movementSpeed = flattenedVelocity.magnitude - DCLCharacterController.i.movingPlatformSpeed;
        else
            blackboard.movementSpeed = flattenedVelocity.magnitude;

        Vector3 rayOffset = Vector3.up * RAY_OFFSET_LENGTH;
        //NOTE(Brian): isGrounded?
        blackboard.isGrounded = Physics.Raycast(target.transform.position + rayOffset,
            Vector3.down,
            RAY_OFFSET_LENGTH - ELEVATION_OFFSET,
            DCLCharacterController.i.groundLayers);

#if UNITY_EDITOR
        Debug.DrawRay(target.transform.position + rayOffset, Vector3.down * (RAY_OFFSET_LENGTH - ELEVATION_OFFSET), blackboard.isGrounded ? Color.green : Color.red);
#endif

        lastPosition = velocityTargetPosition;
    }

    void State_Init(BlackBoard bb)
    {
        if (bb.isGrounded == true)
        {
            currentState = State_Ground;
        }
        else
        {
            currentState = State_Air;
        }
    }

    void State_Ground(BlackBoard bb)
    {
        if (bb.deltaTime <= 0)
        {
            Debug.LogError("deltaTime should be > 0", gameObject);
            return;
        }

        animation[currentLocomotions.run.name].normalizedSpeed = bb.movementSpeed / bb.deltaTime * bb.runSpeedFactor;
        animation[currentLocomotions.walk.name].normalizedSpeed = bb.movementSpeed / bb.deltaTime * bb.walkSpeedFactor;

        float movementSpeed = bb.movementSpeed / bb.deltaTime;

        if (movementSpeed > runMinSpeed)
        {
            animation.CrossFade(currentLocomotions.run.name, RUN_TRANSITION_TIME);
        }
        else if (movementSpeed > walkMinSpeed)
        {
            animation.CrossFade(currentLocomotions.walk.name, WALK_TRANSITION_TIME);
        }
        else
        {
            animation.CrossFade(currentLocomotions.idle.name, IDLE_TRANSITION_TIME);
        }

        if (!bb.isGrounded)
        {
            currentState = State_Air;
            Update(bb.deltaTime);
        }
    }

    void State_Air(BlackBoard bb)
    {
        if (bb.verticalSpeed > 0)
        {
            animation.CrossFade(currentLocomotions.jump.name, JUMP_TRANSITION_TIME, PlayMode.StopAll);
        }
        else
        {
            animation.CrossFade(currentLocomotions.fall.name, FALL_TRANSITION_TIME, PlayMode.StopAll);
        }

        if (bb.isGrounded)
        {
            animation.Blend(currentLocomotions.jump.name, 0, AIR_EXIT_TRANSITION_TIME);
            animation.Blend(currentLocomotions.fall.name, 0, AIR_EXIT_TRANSITION_TIME);
            currentState = State_Ground;
            Update(bb.deltaTime);
        }
    }

    internal void State_Expression(BlackBoard bb)
    {
        var animationInfo = animation[bb.expressionTriggerId];
        animation.CrossFade(bb.expressionTriggerId, EXPRESSION_TRANSITION_TIME, PlayMode.StopAll);

        var mustExit = Math.Abs(bb.movementSpeed) > Mathf.Epsilon || animationInfo.length - animationInfo.time < EXPRESSION_TRANSITION_TIME || !bb.isGrounded;
        if (mustExit)
        {
            animation.Blend(bb.expressionTriggerId, 0, EXPRESSION_TRANSITION_TIME);
            bb.expressionTriggerId = null;
            if (!bb.isGrounded)
                currentState = State_Air;
            else
                currentState = State_Ground;

            Update(bb.deltaTime);
        }
        else
        {
            animation.Blend(bb.expressionTriggerId, 1, EXPRESSION_TRANSITION_TIME / 2f);
        }
    }

    public void SetExpressionValues(string expressionTriggerId, long expressionTriggerTimestamp)
    {
        if (animation == null)
            return;

        if (string.IsNullOrEmpty(expressionTriggerId))
            return;

        if (animation.GetClip(expressionTriggerId) == null)
            return;

        var mustTriggerAnimation = !string.IsNullOrEmpty(expressionTriggerId) && blackboard.expressionTriggerTimestamp != expressionTriggerTimestamp;

        blackboard.expressionTriggerId = expressionTriggerId;
        blackboard.expressionTriggerTimestamp = expressionTriggerTimestamp;

        if (mustTriggerAnimation)
        {
            if (!string.IsNullOrEmpty(expressionTriggerId))
            {
                animation.Stop(expressionTriggerId);
            }

            currentState = State_Expression;
            Update();
        }
    }

    public void Reset()
    {
        if (animation == null)
            return;

        //It will set the animation to the first frame, but due to the nature of the script and its Update. It wont stop the animation from playing
        animation.Stop();
    }

    public void SetIdleFrame() { animation.Play(currentLocomotions.idle.name); }

    public void PrepareLocomotionAnims(string bodyshapeId)
    {
        if (bodyshapeId.Contains(WearableLiterals.BodyShapes.MALE))
        {
            currentLocomotions = maleLocomotions;
        }
        else if (bodyshapeId.Contains(WearableLiterals.BodyShapes.FEMALE))
        {
            currentLocomotions = femaleLocomotions;
        }

        EquipEmote(currentLocomotions.idle.name, currentLocomotions.idle);
        EquipEmote(currentLocomotions.walk.name, currentLocomotions.walk);
        EquipEmote(currentLocomotions.run.name, currentLocomotions.run);
        EquipEmote(currentLocomotions.jump.name, currentLocomotions.jump);
        EquipEmote(currentLocomotions.fall.name, currentLocomotions.fall);
    }

    // AvatarSystem entry points
    public bool Prepare(string bodyshapeId, GameObject container)
    {
        if (!container.transform.TryFindChildRecursively("Armature", out Transform armature))
        {
            Debug.LogError($"Couldn't find Armature for AnimatorLegacy in path: {transform.GetHierarchyPath()}");
            return false;
        }
        Transform armatureParent = armature.parent;
        animation = armatureParent.gameObject.GetOrCreateComponent<Animation>();
        armatureParent.gameObject.GetOrCreateComponent<StickerAnimationListener>();

        PrepareLocomotionAnims(bodyshapeId);
        SetIdleFrame();
        animation.Sample();
        InitializeAvatarAudioAndParticleHandlers(animation);
        return true;
    }

    public void PlayEmote(string emoteId, long timestamps) { SetExpressionValues(emoteId, timestamps); }

    public void EquipEmote(string emoteId, AnimationClip clip)
    {
        if (animation.GetClip(emoteId) != null)
            animation.RemoveClip(emoteId);
        animation.AddClip(clip, emoteId);
    }

    public void UnequipEmote(string emoteId)
    {
        if (animation.GetClip(emoteId) == null)
            return;
        animation.RemoveClip(emoteId);
    }

    private void InitializeAvatarAudioAndParticleHandlers(Animation createdAnimation)
    {
        //NOTE(Mordi): Adds handler for animation events, and passes in the audioContainer for the avatar
        AvatarAnimationEventHandler animationEventHandler = createdAnimation.gameObject.AddComponent<AvatarAnimationEventHandler>();
        AudioContainer audioContainer = transform.GetComponentInChildren<AudioContainer>();
        if (audioContainer != null)
        {
            animationEventHandler.Init(audioContainer);

            //NOTE(Mordi): If this is a remote avatar, pass the animation component so we can keep track of whether it is culled (off-screen) or not
            AvatarAudioHandlerRemote audioHandlerRemote = audioContainer.GetComponent<AvatarAudioHandlerRemote>();
            if (audioHandlerRemote != null)
            {
                audioHandlerRemote.Init(createdAnimation.gameObject);
            }
        }

        animEventHandler = animationEventHandler;
    }

    private void OnDestroy()
    {
        if (animEventHandler != null)
            Destroy(animEventHandler);
    }
}