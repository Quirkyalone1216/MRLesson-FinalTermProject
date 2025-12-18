# MR 桌面塔防（Technical Spike）

本專案為 Meta Quest MR 的桌面塔防小遊戲之技術穿刺版本：  
- 在桌面（水平面）隨機生成怪物  
- 玩家使用射線槍擊殺怪物  
- 怪物碰到高塔會扣血  
- 高塔血量歸零即結束

## 目錄
- `Assets/` Unity 資產與場景
- `docs/` SDD 與 Spike 報告

## 開發環境（請依實際版本補齊）
- Unity 2022.3 LTS
- Meta XR SDK + MRUtilityKit（MRUK）
- 測試裝置：Meta Quest（建議 Quest 3）

## 如何執行（Spike）
1. 開啟 `Assets/Scenes/SampleScene.unity`
2. 確認場景中存在：
   - `Ghost Spawner`（掛 `GhostSpawner`）
   - `RayGun`（槍物件，掛 `RayGun`，shootingPoint 指向槍口）
   - `Tower`（掛 `TowerHealth`）
3. Build And Run 至 Quest，進入 MR 模式並完成掃描。

## 文件
- `docs/SDD.md`
- `docs/TechnicalSpike.md`
