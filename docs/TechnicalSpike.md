# docs/TechnicalSpike.md — Technical Spike 報告（MR 桌面放置 + 射線射擊 + 塔防收斂）

> 文件目的：把 Spike 寫成「可被檢驗的實驗」：方法可重做、指標可量測、結果可被第三方驗證。  
> 建議用於：期末驗收、口試、或後續擴展成正式專案的技術基線。

---

## 1. Spike 研究問題（Research Questions）
本 Spike 驗證以下技術點在 Meta Quest MR 上具備**可重現性**與**可驗收性**，能支撐桌面塔防核心玩法：

1) MR 場景理解（桌面/水平面取樣）  
2) 桌面放置（塔位置穩定且在玩家前方）  
3) 右手射線槍互動（命中、回饋、傷害）  
4) 塔防規則（怪→塔扣血→Game Over 收斂）

---

## 2. 實驗設定（Reproducibility）

### 2.1 專案版本（Source of Truth）
- Unity：`6000.2.4f1`
- URP：`17.2.0`
- Meta XR SDK：`81.0.0`
- MRUK：`81.0.0`
- XR：OpenXR `@1.15.1` + XR Management `@4.5.3`
- 場景：`Assets/Scenes/SampleScene.unity`

### 2.2 測試裝置與環境（請填入）
- 測試裝置：Quest ________（Quest 2 / Quest 3 / Pro）
- OS / Runtime：________
- 光源條件：________（例如室內白光、背光強弱）
- 桌面材質：________（木桌/玻璃桌；反光程度）
- Room Capture 耗時：約 ________ 秒
- 測試次數 N：________（建議 N≥10；若要做成功率與失敗分類，建議 N≥20）

---

## 3. 方法（Methods）與實作對應（Implementation Mapping）

### 3.1 MR 平面理解（MRUK）
**做法**
- 等待 `MRUK.Instance.IsInitialized` 且 `CurrentRoom` 可用
- 以 `GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, ...)` 取樣朝上的水平面
- Label bitmask：以 `TABLE | OTHER` 容錯處理桌面標記不穩（Scene bitmask：`136`）

**對應實作**
- `Assets/Scripts/TowerPlacer.cs`
- `Assets/Scripts/GhostSpawer.cs`（類別 `GhostSpawner`）

---

### 3.2 桌面放置（TowerPlacer：warmup + retry 抗漂移）
**問題**
MR 專案常見「初期追蹤/世界鎖定未穩時就放置」，導致物件數秒後漂移到遠處。

**策略（本專案）**
- Warmup：等待 `warmupSeconds` 後才放置
- Retry：多次取樣候選點，挑選玩家前方、距離合理、離邊距足夠的點位
-（可選）Fallback：桌面 label 不可用時退回地板（是否啟用需明確定義）

**SampleScene 實際參數（序列化值）**
- `warmupSeconds = 1.5`
- `autoRetries = 20`
- `retryInterval = 0.25`
- `attempts = 800`
- `targetLabels` bitmask：`136`

---

### 3.3 桌面生成（GhostSpawner：label 容錯 + 上限控制）
**策略**
- 每 `spawnTimer` 秒嘗試生成一次
- 每次生成最多嘗試 `attemptsPerSpawn` 次取樣點
- `maxAlive` 限制同時存在數，避免效能發散
- `lifeTime` 讓敵人自然淘汰（避免無限累積；若為 0 表示不淘汰）

**SampleScene 實際參數（序列化值）**
- `spawnTimer = 1`
- `attemptsPerSpawn = 32`
- `maxAlive = 25`
- `lifeTime = 0`（0=不淘汰）

---

### 3.4 射線槍互動（RayGun：overlap + spherecast 提升命中）
**問題**
VR/MR 射線射擊在近距離常因 collider 錯過、追蹤抖動或射線太細而 miss。

**策略（本專案）**
- 以 `closeRangeOverlapRadius` 做近距離 overlap（提高近距離命中）
- 以 `beamRadius` 做 spherecast（提高射線厚度與抗 jitter）
- 遮擋：`occlusionMask`（Tower）避免穿透不合理命中

**SampleScene 實際參數（序列化值）**
- `shootingButton = OVRInput.RawButton.RIndexTrigger`
- `beamRadius = 0.03`
- `closeRangeOverlapRadius = 0.05`
- `maxLineDistance = 5`
- `piercing = 1`
- `damagePerShot = 1`
- `layerMask bits = 8`（Enemy）
- `occlusionMask bits = 64`（Tower）

> 技術債註記：目前 RayGun 為相容性以反射尋找 `TakeDamage` 或 HP 欄位；正式化建議改為 `IDamageable` 介面並移除反射（避免 IL2CPP/AOT 風險）。

---

### 3.5 塔防規則（扣血與 Game Over 收斂）
- `GhostEnemy` 觸塔 → `TowerHealth.TakeDamage(damage)`
- `TowerHealth` HP=0 → 觸發 `Died` event
- `GameManager` 監聽事件 → 停用 spawner/gun、清場、顯示 Game Over UI
- `RestartGame()` 由 UI Button 呼叫以重開 scene（避免狀態殘留）

---

## 4. 量測（Metrics）與 Protocol
> 若 Spike 報告沒有「量測與判準」，評分者無法判定你是「做出來」還是「偶然能跑」。本章提供最小可交付的 protocol。

### 4.1 放置成功率（Placement Success Rate）
- 成功定義：完成掃描後 10 秒內，塔仍在玩家前方桌面合理距離（不漂移到遠處/地板/空中）
- 量測：重複啟動 N 次（建議 N≥20），記錄成功/失敗與失敗原因分類
- 失敗分類（建議）：漂移、落地板、落空中、離玩家過遠、未放置

### 4.2 生成成功率（Spawn Success Rate）
- 成功定義：在 Running 狀態下 60 秒觀察窗內至少生成 1 隻；且同時存在數不超過 `maxAlive`
- 量測：觀察 60 秒，記錄成功生成次數 / 嘗試生成次數；並記錄失敗原因（label 不穩/取樣失敗/被擋）

### 4.3 命中率（Hit Rate）
- 成功定義：射擊命中造成扣血/擊殺即算命中
- 建議分距離：近距離（<0.5m）、中距離（0.5–2m）
- 量測：每距離各射擊 30 發，記錄命中數與 miss 情境（抖動/遮擋/穿模）

### 4.4 Game Over 收斂正確性
- 成功定義：塔 HP=0 時必須同時滿足  
  1) 不再生成 Ghost（Spawner disabled）  
  2) 不再接受射擊輸入（RayGun disabled）  
  3) 場上 Ghost 被清除（Destroy）  
  4) Game Over UI 顯示  
- 量測：至少測 3 次；若不穩定，必須說明重現條件與修正策略。

---

## 5. 結果（請用實測填入）
> 若暫無數據請標註 TBD，並寫「為何沒量到」與「下一步如何量」。

| 指標 | 次數/區間 | 成功/總數 | 百分比 | 主要失敗原因（前 1–2 名） |
|---|---:|---:|---:|---|
| 放置成功率 | __ 次啟動 | __ / __ | __% | __________ |
| 生成成功率 | 60 秒觀察 × __ 次 | __ / __ | __% | __________ |
| 命中率（近） | 30 發 × __ 次 | __ / __ | __% | __________ |
| 命中率（中） | 30 發 × __ 次 | __ / __ | __% | __________ |
| Game Over 收斂 | __ 次 | 成功/失敗 | - | __________ |

---

## 6. 討論（Interpretation）
- 你們的數據是否支持「可重現/可驗收」？若不支持，瓶頸更像是 MR label、tracking 漂移、或射線命中？
- 若成功，請指出關鍵設計是哪一個參數/策略（warmup、retry、label 容錯、overlap/spherecast）。
- 若 `lifeTime=0` 且仍穩定，請說明你們如何確保效能不發散（例如 maxAlive、清場、或測試時間短）。

---

## 7. 效度威脅（Threats to Validity）
- 光線/反光、桌面材質、桌面大小與邊界會影響 label 與取樣成功率
- 啟動後前 5–15 秒 tracking 行為可能不同（影響漂移）
- 不同裝置/OS 版本會影響 Room Capture 與 MRUK 行為
- 測試者站位與手部抖動會影響射擊命中率（尤其近距離）

---

## 8. 下一步（Next Steps）
- 量測自動化：把成功率/命中率寫入 CSV/JSON（便於報告）
- 技術債清理：RayGun 移除反射、導入 object pooling
- 視覺策略：Passthrough 下材質/深度策略一致化（避免 runtime 改材質造成資源膨脹）
- 上架準備：targetSdkVersion 升級至 34（2026-03-01 政策）

---

## 9. 影像證據（建議）
建議放 3–4 段短影片到 `docs/media/`（每段 10–20 秒）：
- `docs/media/spike_place.mp4`（放置成功與不漂移）
- `docs/media/spike_spawn.mp4`（桌面生成）
- `docs/media/spike_shoot.mp4`（射擊命中與擊殺）
- `docs/media/spike_gameover.mp4`（扣血至 Game Over 收斂）

