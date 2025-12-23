# Technical Spike 報告（MR 桌面放置 + 射線射擊 + 塔防規則）

## 1. Spike 目的
本 Spike 的目的在於確認下列技術在 Meta Quest MR 上可穩定運作，並能支撐期末塔防核心玩法：
1) MR 場景理解（桌面/水平面）  
2) 桌面放置（塔位置穩定且在玩家面前）  
3) 右手射線槍互動（命中、回饋、傷害）  
4) 塔防規則（怪→塔扣血→Game Over）

## 2. 核心技術點與對應實作
### 2.1 MR 平面理解（MRUK）
- 取得 `MRUK.Instance` 與 `CurrentRoom`
- 以 `GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, ...)` 取樣朝上的水平面點位
- Label 以 bitmask 篩選（建議 TABLE + OTHER 容錯）

對應檔案：
- `Assets/Scripts/GhostSpawer.cs`（類別：GhostSpawner）
- `Assets/Scripts/TowerPlacer.cs`

### 2.2 桌面放置（避免初期漂移的關鍵）
問題：MR 專案常見「一開始放對，幾秒後整個物件跑遠」的漂移現象，原因多為 tracking origin/world lock 尚未穩定就放置。

對策（本專案）：
- `TowerPlacer` 在 MRUK ready 後等待 warmupSeconds
- 之後多次 retry（autoRetries），挑選「你面前、距離適中、且不低於相機太多」的桌面點放置塔

對應檔案：
- `Assets/Scripts/TowerPlacer.cs`

### 2.3 射線槍互動（命中穩定性）
- 右手扳機：OVRInput（RIndexTrigger）
- 命中策略：
  - 近距離 `OverlapSphere`（避免槍口貼近 collider 時 raycast/spherecast 異常）
  - 中遠距離 `SphereCastAll`（beamRadius 提升命中穩定）
  - `occlusionMask`（Tower 遮擋，避免穿塔打到背後敵人）
  - `piercing`（可選：同一槍穿透多個敵人）

對應檔案：
- `Assets/Scripts/RayGun.cs`

### 2.4 塔防規則與 Game Over
- `GhostEnemy` 朝塔移動，觸塔扣血並自毀
- `TowerHealth` 血量歸零觸發 `Died` 事件
- `GameManager` 接管 Game Over：停用 spawner/gun、清場

對應檔案：
- `Assets/Scripts/GhostEnemy.cs`
- `Assets/Scripts/TowerHealth.cs`
- `Assets/Scripts/GameManager.cs`

## 3. Repro Steps（助教可照做）
1. Build & Run 到 Quest  
2. 進入 MR 模式並完成掃描  
3. 觀察塔：應自動出現在「你面前的桌面」上  
4. 觀察生成：桌面上應持續生成 Ghost  
5. 射擊：扣右手扳機，命中 Ghost 應造成傷害/擊殺（有特效/音效）  
6. 扣血：放任 Ghost 碰塔，塔 HP 下降  
7. HP=0：Game Over（停止生成與射擊、清場）

## 4. 觀測結果（請填入你們實測數據；格式先固定，便於評分）
- 測試裝置：__________（Quest 2 / Quest 3 / 其他）
- 測試環境：室內光源 ________；桌面材質 ________；掃描耗時約 ________ 秒
- 放置穩定性：
  - warmupSeconds = ________
  - autoRetries = ________
  - 是否出現「放置後漂移/跑遠」：有 / 無
- 生成穩定性：
  - spawnTimer = ________
  - 是否出現生成穿模/掉落：有 / 無
- 射擊命中穩定性：
  - beamRadius = ________
  - 近距離是否容易 miss：是 / 否
- 性能（建議簡述即可）：
  - 目視 FPS：穩定 / 偶發掉幀（情境：__________）
  - 同時怪物數上限 maxAlive：__________

## 5. 已知限制與下一步（非 Spike 必做，但可作為期末加分方向）
- UI：加入 Game Over 面板、重新開始流程、分數/波次
- 難度：波次生成、速度與血量曲線、生成點避障與分散策略
- MR 互動：更明確的桌面可視化提示、掃描狀態指示
- 視覺：更一致的材質/透明與深度策略（Passthrough 下的可讀性）

## 6. 影像證據（建議）
- `docs/media/spike_spawn.mp4`
- `docs/media/spike_shoot.mp4`
- `docs/media/spike_tower_damage.mp4`
