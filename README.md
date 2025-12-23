# MR 桌面塔防《Ghost Hunter》（Technical Spike）

本專案為 Meta Quest MR 的桌面塔防小遊戲之技術穿刺（Technical Spike）版本，目標是以「可在真實桌面上穩定放置、可射擊互動、可扣塔血量並觸發 Game Over」作為最小可驗收核心。

## 核心玩法（對照期末規格）
1. 怪物會在桌面（水平面）隨機生成  
2. 玩家使用右手射線槍擊殺怪物  
3. 怪物碰到高塔會扣血  
4. 高塔血量歸零即結束（停止生成與射擊、清除場上怪物）

## 專案結構
- `Assets/`：Unity 資產、場景、腳本、Prefab
- `docs/`：設計文件（SDD）與 Spike 報告
- `Packages/`、`ProjectSettings/`：專案設定（版本與可重現依據）

## 開發環境（以專案檔案為準）
- Unity：`6000.2.4f1`
- Meta XR SDK：`com.meta.xr.sdk.all@81.0.0`
- XR：OpenXR + XR Management
- Render Pipeline：URP 17.x
- 目標裝置：Meta Quest 系列（以你們實測型號為準）

## 如何執行（Spike）
1. 開啟場景：`Assets/Scenes/SampleScene.unity`
2. 進入 Play/Build 前，請確認場景中至少有下列物件（名稱可不同，但功能需存在）：
   - `GameManager`（掛 `GameManager`）
   - `Tower`（掛 `TowerHealth`，並掛 `TowerPlacer` 用於桌面放置）
   - `Ghost Spawner`（掛 `GhostSpawner`；注意：腳本檔名為 `Assets/Scripts/GhostSpawer.cs`，類別名稱為 `GhostSpawner`）
   - 右手控制器底下的 `Ray Gun`（掛 `RayGun`，且 `shootingPoint` 指向槍口）
3. 重要設定（Layer / Mask）
   - Layer 3：`Enemy`（Ghost prefab 在此 layer）
   - Layer 6：`Tower`（Tower 在此 layer，RayGun 會用 occlusionMask 遮擋）
4. Build And Run 到 Quest
5. 進入 MR 模式並完成掃描（場景理解 / room capture）
6. 驗收觀察：
   - 塔會被放到「你面前的桌面」上（由 `TowerPlacer` warmup + retry 保證穩定）
   - 桌面上會持續生成 Ghost（由 `GhostSpawner` 取樣水平面生成）
   - 扣扳機射擊：命中 Ghost 會造成傷害並擊殺
   - Ghost 接觸到塔：塔扣血；塔血量歸零後 Game Over（停用 spawner/gun 並清場）

## 文件
- `docs/SDD.md`：Software Design Document（設計與可驗收規格）
- `docs/TechnicalSpike.md`：Technical Spike 報告（技術點、步驟、結果、限制）
