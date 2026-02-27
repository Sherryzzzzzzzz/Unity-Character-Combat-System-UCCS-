# Tasks: archive/2026-02-26-full-gas-system

This file lists ordered implementation tasks for stabilizing the GAS system. Tasks are grouped by priority and mapped to files and approximate line ranges where modifications are expected.

High priority

1. GameplayAbility.CommitAbility atomic behavior
- Files: Assets/Scripts/GASSystem/GameplayAbility.cs
- Lines: ~80-115
- Work: Verify CommitAbility returns bool on success/failure. Ensure CommitAbility performs cost apply transactionally and does not start cooldown or mark currentAbility if cost apply fails or throws. Add unit tests (PlayMode) for commit atomicity.

2. TryActivate and ActivateInternal respect commit result; guard TagComponent/OwnerASC usage
- Files: Assets/Scripts/GASSystem/GameplayAbility.cs
- Lines: ~114-160
- Work: Ensure TryActivate checks TagComponent presence per plan; ActivateInternal must only set current ability and grant tags after CommitAbility returns true. Surround Activate() call with try/catch and in case of exception, ensure granted tags/cooldown are rolled back and method returns false.

3. Add try/catch around ApplyGameplayEffect call sites
- Files: Assets/Scripts/GASSystem/GameplayAbility.cs (where costEffect applied), Assets/Scripts/GASSystem/AbilitySystemComponent.cs (external calls), Assets/Scripts/GASSystem/GASSystemTest.cs
- Lines: around CommitAbility usage and ApplyEffectSpec call sites
- Work: Wrap calls that may execute external scripts/cues in try/catch and on exception ensure partial application is rolled back where possible and a clean failure returned.

4. Defensive null checks when removing modifiers
- Files: Assets/Scripts/GASSystem/AbilitySystemComponent.cs
- Lines: RemoveActiveEffectInternal (~line 350-400)
- Work: Null-check AttributeSet/AttributeValue/RegisteredModifier before calling RemoveModifier. If a RegisteredModifier is missing, attempt to continue removing other modifiers and log a warning.

5. Add PlayMode tests for cost failure, ability commit, and End behavior
- Files: Tests/PlayMode/GAS/AbilityCommitTests.cs (new)
- Work: Implement PlayMode tests that create a test ASC, an ability with cost effect that will be rejected, verify CommitAbility returns false and no cooldown/current ability state changes. Test End() cleanup of granted tags.

Medium priority

6. Change effect lookup to handle multiple instances per SO
- Files: Assets/Scripts/GASSystem/AbilitySystemComponent.cs, ActiveGameplayEffect.cs
- Lines: ApplyDurationEffect (~200-260), _effectLookup usages throughout (~lines 260-380)
- Work: Add _activeEffectsByHandle: Dictionary<int, ActiveGameplayEffect> and _effectHandleGroups: Dictionary<GameplayEffect, List<int>>. Update ApplyDurationEffect to create new instances even if an effect SO exists; consult stackingPolicy to decide whether to refresh/AddStacks/deny.

7. Transactional ApplyDurationEffect with rollback
- Files: Assets/Scripts/GASSystem/AbilitySystemComponent.cs
- Lines: ApplyDurationEffect
- Work: Build appliedModifiers and appliedTags lists, and only commit to ASC after all succeed. On failure, rollback modifiers/tags and return -1.

8. TargetData and ApplyGameplayEffect overloads
- Files: Assets/Scripts/GASSystem/AbilitySystemComponent.cs, GameplayEffectSpec.cs
- Lines: MakeEffectSpec overloads and GetMagnitude methods (~lines 60-120)
- Work: Add TargetData struct, overloads for ApplyGameplayEffect(effect, instigatorASC, targetASC/targetData), and make GetMagnitude accept optional targetASC/targetData to support target-based calculations.

9. Add TagQuery type and utilities
- Files: Assets/Scripts/GASSystem/TagComponent.cs
- Lines: HasTag/ConsumeTag functions (~current ranges)
- Work: Implement TagQuery with requiredAny/requiredAll/blockedAny and EvaluateTagQuery. Update effect application checks to use Query where appropriate.

Low priority

10. GameplayCueManager safety wrappers
- Files: Assets/Scripts/GASSystem/GameplayCueManager.cs
- Lines: ExecuteCue/AddCue/RemoveCue
- Work: Wrap cue dispatch in try/catch and optionally support lookup across GameObject hierarchy.

11. ASC Inspector Editor script
- Files: Assets/Editor/GAS/AbilitySystemComponentInspector.cs (new)
- Work: Implement a small inspector showing _activeEffectsByHandle, timeRemaining, stacks, and registered modifiers for debugging.

12. Performance/GC review
- Work: After functional changes and tests, profile critical paths and reduce allocations where high-frequency.

Verification & Tests

- Add PlayMode and EditMode tests described in the design. Map tests to change artifacts and ensure CI can run them.

Notes

- Where exact line numbers are listed above, they are approximate and intended to guide edits. Use repo search to locate precise locations.
- Changes will be implemented on a new branch per the plan. Diffs and summaries will be provided for review before merging.

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
