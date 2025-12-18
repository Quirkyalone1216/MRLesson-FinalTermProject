# Technical Spike 報告（MR 桌面生成 + 射線射擊）

## 1. Spike 目的
本 Spike 的目的在於確認「MR 桌面辨識與物件放置」以及「射線槍互動」在 Meta Quest 上可正常運作，足以支撐期末塔防核心玩法。

## 2. 核心技術點
1. MR 平面理解：以 Meta XR MRUtilityKit（MRUK）取得房間表面，於水平面（桌面）隨機取樣生成點位。  
2. MR 物件放置：在取樣點位 Instantiate Ghost Prefab，並做適當法線偏移與 Upright 旋轉修正。  
3. 互動射擊：右手扳機輸入 → Raycast → 命中判定 → 特效/音效回饋 → 對敵人造成傷害。  
4. 塔防規則：敵人朝塔移動，碰觸塔時扣血；塔血量歸零 Game Over。

## 3. 實作對應（專案檔案）
- 生成：`Assets/Scripts/GhostSpawer.cs`（改為 HORIZONTAL surface）  
- 射擊：`Assets/Scripts/RayGun.cs`（加入 IDamageable 傷害呼叫）  
- 敵人：新增 `Assets/Scripts/GhostEnemy.cs`  
- 高塔：新增 `Assets/Scripts/TowerHealth.cs`  
- 場景：`Assets/Scenes/SampleScene.unity`（新增 Tower 物件並配置）

## 4. 實驗步驟（Repro Steps）
1. 佩戴 Quest，啟動應用程式並完成 MR 空間掃描（桌面需可辨識）。  
2. 觀察桌面：每隔固定時間生成 Ghost。  
3. 按下右手扳機：射線與命中特效出現；命中 Ghost 後 Ghost 被擊殺。  
4. 放任 Ghost 走向 Tower：Ghost 觸塔後 Tower HP 下降。  
5. 重複直到 HP=0：顯示 Game Over（或 Console 訊息），並停止生成與射擊。

## 5. 成果與結論
- 已證實：MR 桌面取樣生成與射線互動可於 Quest 上運作，能支撐期末核心玩法。  
- 下一步（非 Spike 必要）：加入 UI（HP bar / GameOver Panel）、波次生成、難度曲線、分數統計與音效強化。

## 6. 影像證據（建議）
- 在 `docs/media/` 放置：
  - `spike_spawn.mp4`：桌面生成成功
  - `spike_shoot.mp4`：射擊命中與擊殺
  - `spike_tower_damage.mp4`：觸塔扣血與 GameOver
