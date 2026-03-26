Stagger GameplayEffect (example)

Purpose

A minimal example configuration for a short-duration Stagger effect designers can create in-editor.

Suggested fields (create a GameplayEffect asset and set values accordingly):

- durationPolicy: Duration
- duration: 0.8
- period: 0 (no periodic ticks)
- stackingPolicy: RefreshDuration
- maxStacks: 1
- grantedTags:
  - state.stagger (GameplayTagSO)
- applicationRequiredTags: []
- applicationBlockedTags:
  - state.immunity.stagger (GameplayTagSO)  // optional: put this tag on actors to make them immune
- modifiers: []
- cueTag: (optional) gameplay cue tag for stagger VFX/SFX

Notes

- The GameplayEffect should grant a short-lived tag (state.stagger) so runtime systems (AbilitySystemComponent) or Animation/GameplayCue handlers can respond to the stagger.
- Use applicationBlockedTags to express immunity (state.immunity.stagger). The existing ApplyEffectSpec checks applicationBlockedTags and will reject apply if the target has the tag.
- Designers should create a GameplayTagSO asset named "state.stagger" and optionally "state.immunity.stagger" and reference them here.

Example (editor steps)

1. Right-click in the Project window > Create > GAS-like > GameplayEffect
2. Name asset: StaggerExample
3. Set Duration Policy = Duration, Duration = 0.8
4. Add a granted tag: reference your GameplayTagSO for state.stagger
5. (Optional) Add applicationBlockedTags entry referencing state.immunity.stagger
6. Save

This file is an editor-facing example; it is not a binary .asset file. Create the actual GameplayEffect asset in the Unity editor using the values above.
