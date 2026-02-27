# Design: archive/2026-02-26-full-gas-system

Overview

This design documents concrete API changes, data model decisions, and strategies for stabilizing the project's GAS for single-player usage. The design aims to be minimally invasive: preserve existing data formats (GameplayEffect SOs, stacking/duration semantics) while fixing runtime lifecycle, atomicity, and null-safety bugs.

Key design decisions

1. Runtime identity
- ActiveGameplayEffect.Handle will remain the primary runtime identifier (int). A new handle-indexed map will be the primary lookup: Dictionary<int, ActiveGameplayEffect> _activeEffectsByHandle.
- We will also maintain a secondary grouping map Dictionary<GameplayEffect, List<int>> _effectHandleGroups to support stackingPolicy decisions and lookups by asset. This allows multiple runtime instances of the same GameplayEffect SO (from different instigators) while still supporting stacking rules.

2. Apply API surface
- AbilitySystemComponent.ApplyGameplayEffect(GameplayEffect effect, AbilitySystemComponent instigatorASC, AbilitySystemComponent targetASC) will be added as a convenience overload.
- ApplyEffectSpec(GameplayEffectSpec spec) will continue to be the main implementation. ApplyEffectSpec will return int: -1 on rejection, 0 for instant effects, and >0 for duration/infinite effect handles.
- MakeEffectSpec overloads will accept optional TargetData or targetASC to allow target-relative magnitude resolution.

3. Atomicity & transactions
- ApplyDurationEffect will register attribute modifiers and grant tags transactionally: it will collect applied modifiers and tags in temporary lists, and only commit to the ASC's persistent state once all steps succeed. On any failure it will rollback all prior changes before returning -1.
- CommitAbility will return bool. GameplayAbility.ActivateInternal will only proceed when CommitAbility returns true.
- All calls that execute external cues/ability code will be wrapped in try/catch to prevent exceptions from leaving partial state.

4. Magnitude semantics
- GameplayEffectSpec captures attacker attributes during creation (CapturedAttackerAttributes). For AttributeBased magnitudes with captureSource==Attacker, GetMagnitude will use the captured snapshot regardless of later changes to the instigator.
- For captureSource==Target, GetMagnitude(targetASC) will read the targetASC's current AttributeSet values at the time of evaluation (realtime).
- IMagnitudeCalculation implementations will be executed with a reference to the spec and optional targetASC; they must be defensive but will be invoked inside try/catch to avoid runtime exceptions bubbling up.

5. Tag semantics
- TagComponent will expose HasTag, HasTagOrChild, AddTag, RemoveTag, AddTransientTag, ConsumeTag, and a new EvaluateTagQuery(TagQuery) helper. TagComponent is optional: when absent, ActivationRequiredTags are considered empty (allowed), but ActivationBlockedTags are only checked if TagComponent exists.

6. Defensive null-safety
- All critical code paths will guard against missing TagComponent, AttributeSet, or OwnerASC and fail gracefully (return false or -1) rather than throwing NullReferenceException.
- Removing modifiers will null-check AttributeValue and RegisteredModifier handles and tolerate partially applied state (attempt best-effort rollback and log warnings).

7. Cues and external callbacks
- GameplayCueManager dispatch will wrap calls to IGameplayCue implementations in try/catch and log exceptions. This prevents cues from leaving effects half-applied.

Files & functions to change (high level)
- Assets/Scripts/GASSystem/GameplayAbility.cs: CommitAbility, TryActivate, ActivateInternal, End
- Assets/Scripts/GASSystem/AbilitySystemComponent.cs: ApplyEffectSpec, ApplyDurationEffect, MakeEffectSpec overloads, RemoveActiveEffectInternal, _activeEffectsByHandle and _effectHandleGroups
- Assets/Scripts/GASSystem/GameplayEffectSpec.cs: GetMagnitude overloads, attribute snapshot behavior
- Assets/Scripts/GASSystem/ActiveGameplayEffect.cs: handle generation, lifetime, stacks, RegisteredModifiers container
- Assets/Scripts/GASSystem/TagComponent.cs: TagQuery helpers and safe tag consumption
- Assets/Scripts/GASSystem/AttributeSet.cs & AttributeValue.*: safe RemoveModifier checks
- Assets/Scripts/GASSystem/GameplayCueManager.cs & IGameplayCue: try/catch wrappers

Testing strategy

1. PlayMode tests
- Ability commit atomicity: create an ability with a cost effect that is rejected, ensure CommitAbility returns false and ability is not marked active nor put on cooldown.
- Effect transactional application: simulate a failure in registering a modifier (mock AttributeValue.RemoveModifier or cause null) and ensure no residual modifiers/tags remain.
- Stacking behavior: apply the same GameplayEffect SO from two different instigators and ensure both ActiveGameplayEffect instances exist and stack rules are applied per-instance.
- Periodic tick correctness: Apply periodic damage effect and verify attribute deltas over time.

2. EditMode tests
- GameplayEffectSpec.GetMagnitude semantics: verify Attacker snapshot vs Target realtime behavior with controlled attribute changes.
- Tag queries: validate HasTag, ConsumeTag, and transient tag lifetimes.

3. Manual QA
- Extend GASSystemTest component to exercise error paths and print active effects/attributes.

Notes on backward compatibility
- Data in GameplayEffect SOs and GameplayAbilitySO remains unchanged.
- Public APIs that previously returned int handles still do; ApplyEffectSpec returns -1 on rejection (existing code paths should already handle -1 in some cases but will be audited).

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
