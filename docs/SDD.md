# MR 桌面塔防《Ghost Hunter》— SDD（Software Design Document）

## 1. 目標與範圍
本專案為 Meta Quest MR（混合實境）上的桌面塔防小遊戲。玩家在真實桌面上以右手射線槍擊殺隨機生成之怪物；怪物接觸高塔則造成扣血，高塔血量歸零即遊戲結束。

本次交付定位為 **Technical Spike**：不追求完整關卡、美術與 UI 完整度，而是驗證「MR 桌面放置 + 射擊互動 + 塔防規則」可在裝置上穩定運行並可重現。

## 2. 開發環境（可重現依據）
- Unity：`6000.2.4f1`（由 `ProjectSettings/ProjectVersion.txt` 定義）
- Meta XR SDK：`com.meta.xr.sdk.all@81.0.0`
- XR：OpenXR + XR Management
- Render Pipeline：URP 17.x
- Build Scene：`Assets/Scenes/SampleScene.unity`

## 3. 使用者故事（User Stories）
- 玩家戴上 Quest 後完成 MR 掃描，系統取得房間/表面資訊並在桌面生成怪物。
- 玩家使用右手射線槍瞄準射擊怪物，命中後怪物受傷/死亡並提供視覺與音效回饋。
- 若怪物接觸高塔，則高塔扣血；當高塔血量歸零時進入 Game Over，停止生成與射擊並清場。

## 4. 核心規格（Gameplay Spec）
### 4.1 規則（對照期末規格）
1. 怪物在桌面（水平面）隨機生成  
2. 玩家以槍枝擊殺怪物  
3. 怪物碰到高塔扣血  
4. 高塔血量歸零遊戲結束  

### 4.2 Spike（最小可驗收）定義
- 放置：塔必須被放到「你面前的桌面」上，避免初始 world lock/追蹤穩定前放置導致位置漂移。
- 生成：固定間隔生成 Ghost，生成點取樣自 MRUK 的水平面（朝上表面）。
- 射擊：右手扳機觸發射線；命中 Enemy layer 的 Ghost 造成傷害。
- 扣血：Ghost 觸塔後扣塔血並自毀。
- 結束：塔血量歸零後，停止 spawner/gun 並清除場上怪物。

## 5. 系統架構（Architecture）
### 5.1 模組列表（以專案檔案為準）
- 生成：`Assets/Scripts/GhostSpawer.cs`（類別：`GhostSpawner`）
  - 使用 MRUK 在水平面取樣生成點位
  - 生成後將 `tower` 引用注入 `GhostEnemy.tower`
- 敵人：`Assets/Scripts/GhostEnemy.cs`
  - 朝塔移動、受傷死亡、觸塔扣血
- 射擊：`Assets/Scripts/RayGun.cs`
  - OVRInput 右手扳機
  - 近距離 overlap + spherecast（支援 piercing 與 occlusion）
- 塔：`Assets/Scripts/TowerHealth.cs`
  - 血量、扣血、死亡事件
- 遊戲狀態：`Assets/Scripts/GameManager.cs`
  - 監聽塔死亡事件：停用 spawner / gun、清除場上敵人
- 桌面放置（關鍵 MR 穩定性）：`Assets/Scripts/TowerPlacer.cs`
  - MRUK 初始化後 warmup，再多次 retry，挑選「你面前」的桌面點放置塔
-（UI）血量顯示：
  - `Assets/Scripts/TowerHealthUI.cs`
  - `Assets/Scripts/GhostHealthUI.cs`

### 5.2 互動與資料流（文字版）
1. MRUK 初始化完成 → 取得 CurrentRoom  
2. `TowerPlacer` warmup（避免座標系尚未穩定）→ 在桌面（TABLE/OTHER）挑選「你面前」的水平點放置 `Tower`  
3. `GhostSpawner` 週期性在水平面取樣點生成 `Ghost.prefab`，並把 `tower` 指派給 `GhostEnemy`  
4. `GhostEnemy` 朝 `tower` 移動；進入塔的 collider 範圍觸發扣血  
5. 玩家扣右手扳機，`RayGun` 發射射線命中 `Enemy` layer → 呼叫 `GhostEnemy.TakeDamage()`  
6. `TowerHealth` 死亡觸發事件 → `GameManager` 執行 Game Over 行為（停用、清場）

## 6. 場景配置（驗收檢查表）
### 6.1 Scene：`Assets/Scenes/SampleScene.unity`
- `Tower`
  - Layer：`Tower`
  - 必須掛：`TowerHealth`、`TowerPlacer`
- `Ghost Spawner`
  - 必須掛：`GhostSpawner`
  - `prefabToSpawn` 指向 `Assets/Prefabs/Ghost.prefab`
  - `tower` 必須指向場景內 `Tower` 的 Transform
- `Ray Gun`（右手控制器下）
  - 必須掛：`RayGun`
  - `shootingPoint` 必須指定槍口 Transform
  - `layerMask` 必須包含 `Enemy`
  - `occlusionMask` 建議包含 `Tower`

### 6.2 Layer 規範（重要）
- Layer 3：`Enemy`（Ghost）
- Layer 6：`Tower`（Tower）
- `RayGun.layerMask` 只打 Enemy；`RayGun.occlusionMask` 只遮擋 Tower  
- 若 layer/mask 不一致，最常見症狀：
  - 打不到怪（mask 沒含 Enemy）
  - 射線被塔/桌面錯誤遮擋（occlusionMask 配置錯）

## 7. 技術風險與對策（Spike 驗證重點）
| 風險 | 典型症狀 | 對策（本專案） |
|---|---|---|
| 桌面 label 不穩 | 掃描後桌面沒有 TABLE label | TowerPlacer / Spawner 使用 label bitmask（建議 TABLE + OTHER）容錯 |
| 座標系未穩 | 一開始放置正確，過幾秒跑遠 | TowerPlacer warmupSeconds + autoRetries，等 tracking/world lock 穩定再放置 |
| 射線命中不穩 | 近距離容易 miss、穿模 | RayGun 使用 overlap + spherecast，並提供 beamRadius/closeRangeOverlapRadius |
| Trigger 不觸發 | 怪碰塔無扣血 | Enemy 有 Rigidbody（kinematic）且 collider trigger；Tower 有 collider |

## 8. 測試計畫（驗收腳本）
- 啟動 → 完成 MR 掃描 → 塔被放到你面前的桌面  
- 桌面持續生成 Ghost（至少 1 隻）  
- 扣扳機射擊 → 命中 Ghost 造成傷害/擊殺  
- 放任 Ghost 碰塔 → 塔 HP 下降  
- HP=0 → Game Over（停用 spawner/gun、清場）

## 9. 交付物（Deliverables）
- Unity Repo（本專案）
- 文件：`README.md`、`docs/SDD.md`、`docs/TechnicalSpike.md`
-（建議）實機證據：`docs/media/*.mp4`
