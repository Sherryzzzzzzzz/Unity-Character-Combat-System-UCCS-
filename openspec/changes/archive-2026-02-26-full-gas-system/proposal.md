# Proposal: Archive and Stabilize GAS for Single-player

Summary

This change archives and aligns the project's Gameplay Ability System (GAS) into a stable, single-player-ready state under the change name: archive/2026-02-26-full-gas-system.

Motivation

Recent exploration found several stability and correctness gaps in the GAS implementation: ability commit atomicity, defensive null checks, effect lifecycle edge cases, effect lookup semantics, and tag/target semantics. These gaps cause intermittent failures, incorrect attribute modifications, and lingering state after failed effect applications. The goal of this change is to harden the system for single-player use, document design decisions, and produce a clear set of tasks to finish implementation and testing.

Acceptance criteria

- Commit atomicity: Abilities apply costs atomically. If cost application is rejected or throws, the ability must not enter cooldown or be marked active.
- Effect lifecycle robustness: Registering and removing ActiveGameplayEffect must not leave residual modifiers or tags when failures occur.
- Null-safety: Critical code paths are guarded against missing components (TagComponent, AttributeSet, OwnerASC) and do not crash.
- Magnitude semantics: AttributeBased magnitudes have unambiguous semantics (attacker snapshot vs target realtime) and GameplayEffectSpec encapsulates attacker snapshot behavior.
- Effect identity: Multiple active instances of the same GameplayEffect SO from different instigators are allowed; ActiveGameplayEffect.Handle is the primary runtime identifier.
- Test coverage: PlayMode/EditMode tests verify commit atomicity, effect lifecycle behaviors, stacking/refresh, transient tag behavior, and periodic ticks.

Deliverables

- openspec/changes/archive-2026-02-26-full-gas-system/proposal.md
- openspec/changes/archive-2026-02-26-full-gas-system/design.md
- openspec/changes/archive-2026-02-26-full-gas-system/tasks.md
- PlayMode/EditMode tests under Tests/ verifying acceptance criteria
- Small Editor inspector at Assets/Editor/GAS/AbilitySystemComponentInspector.cs to view active effects

Scope & Out-of-scope

In-scope
- Stabilize core GAS runtime behavior (ASC, Abilities, Effects, Tags, Cue dispatch)
- Add design documentation and tasks
- Add tests and a lightweight inspector for debugging

Out-of-scope
- Full rearchitecture of GAS for multiplayer or replication
- Performance optimizations beyond obvious low-cost improvements

Next steps

1. Create design and tasks artifacts under openspec/changes/archive-2026-02-26-full-gas-system/
2. Implement high-priority fixes and PlayMode tests in a new local branch
3. Provide diffs and summary for review before merging to main

Co-Authored-By: Claude Opus 4.6 <noreply@anthropic.com>
