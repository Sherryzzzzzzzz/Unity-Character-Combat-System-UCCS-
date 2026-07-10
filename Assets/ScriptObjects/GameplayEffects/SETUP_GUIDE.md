# Unity Editor 设置指南

## 1. 格挡系统配置

### 1.1 给 PlayerModel 配置 guardEffect
1. 选中场景中的 Player GameObject
2. 在 PlayerModel 组件的 `Guard Effect` 字段拖入 `Assets/ScriptObjects/GameplayEffects/GE_GuardStance.asset`
3. 确保 `Guard Animation` 和 `Guard End Animation` 已配置动画Clip

### 1.2 给 HurtBoxManager 配置格挡参数
在 Player 和 Enemy 的 HurtBoxManager 组件上:
- `Block Damage Reduction`: 0.8 (减免80%伤害)
- `Block Poise Cost Base`: 10
- `Guard Break Effect`: 拖入 `GE_GuardBreak.asset`
- `Guard Break Tag`: 可选（state.stagger 已在GE中处理）
- `Block Reaction Animation`: 拖入格挡反应的动画Clip
- `Block Sparks VFX`: 拖入火花粒子预制体
- `Block Sound`: 拖入格挡音效

### 1.3 配置 AttackData 的 staggerEffect
1. 打开 `Assets/ScriptObjects/AttackData/Light1.asset`
2. `Stagger Effect` 字段拖入 `GE_Stagger.asset`
3. 对所有攻击数据 (Light/Heavy/SkyLight等) 重复此操作

### 1.4 配置 Parryable 的 parriedStunBuff
在 Player 和 Enemy 的 Parryable 组件上:
- `Parried Stun Buff` 改为 `Buff_ParriedStun.asset`
  (之前错误地指向了 Buff_Parry_PerfectWindow)

## 2. UI 搭建

### 2.1 HUD Canvas 结构
在场景的 Canvas (Screen Space Overlay) 下创建:

```
Canvas
├── CombatHUD (挂载 CombatHUD.cs)
│   ├── PlayerHealthBar (挂载 PlayerHUDController)
│   │   ├── Background (Image)
│   │   ├── Foreground (Image)
│   │   └── HealthText (Text)
│   ├── PoiseBar (挂载 PoiseBarUI)
│   │   ├── Background (Image)
│   │   └── Foreground (Image)  # 请用橙色
│   ├── SkillSlots
│   │   ├── SkillSlot_Light (挂载 SkillSlotUI)
│   │   ├── SkillSlot_Heavy (挂载 SkillSlotUI)
│   │   ├── SkillSlot_Dodge (挂载 SkillSlotUI)
│   │   └── SkillSlot_Guard (挂载 SkillSlotUI)
│   ├── TargetInfo (挂载 TargetInfoUI)
│   │   ├── TargetName (Text)
│   │   ├── TargetHealthBar (Image)
│   │   └── TargetHealthText (Text)
│   ├── BuffList (挂载 StatusEffectListUI, TagFilter="Buff.")
│   ├── DebuffList (挂载 StatusEffectListUI, TagFilter="Debuff.")
│   ├── DamageNumbers (挂载 DamageNumberManager)
│   ├── LockOnIndicator (Image)
│   ├── ComboText (Text)
│   └── GameOverPanel
│       ├── GameOverTitle (Text "You Died")
│       ├── RetryButton (Button)
│       └── QuitButton (Button)
```

### 2.2 绑定引用
在 CombatHUD 组件的 Inspector 中:
- 将所有子对象拖入对应字段
- GameOver 按钮的 OnClick 已由代码自动绑定

## 3. 材质升级

### 3.1 头发材质
1. 选中 `Assets/ASP/Demo/Models/unity-chan!/Materials/hair.mat`
2. 将 Shader 从 `ASP/Character` 切换为 `ASP/Character/Hair Enhanced`
3. 调整各向异性参数:
   - Aniso Shift 1: 0.25, Width 1: 0.15, Color 1: 暖色
   - Aniso Shift 2: -0.35, Width 2: 0.3, Color 2: 冷色

### 3.2 皮肤材质
1. 选中 `body.mat`, `face.mat`, `skin1.mat`
2. 将 Shader 切换为 `ASP/Character/Skin Enhanced`
3. 调整 SSS 参数:
   - SSS Color: 偏红的暖色 (1, 0.3, 0.2)
   - SSS Strength: 0.5
   - SSS Distortion: 0.4

### 3.3 眼睛/睫毛材质
保持原 `ASP/Character` shader (Face 模式更适合)

## 4. 验证步骤

1. **格挡测试**: 按格挡键 → 确认角色进入格挡姿态 → 让敌人攻击 → 应看到格挡火花/听到音效/血量少量减少
2. **破防测试**: 持续格挡多次攻击 → Poise条归零 → 角色破防硬直
3. **弹反测试**: 在攻击命中瞬间按格挡 → 攻击者被弹反硬直
4. **UI测试**: HUD显示血条/架势条/技能冷却/目标信息
5. **Shader测试**: 头发在光照下显示各向异性高光条纹
