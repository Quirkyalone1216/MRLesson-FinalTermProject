# docs/TechnicalSpike.md — Technical Spike 報告（MR 桌面放置 + 射線射擊 + 塔防規則）

## 1. Spike 目的
本 Spike 旨在驗證以下技術點在 Meta Quest MR 上具備**可重現性**與**可驗收性**，能支撐期末塔防核心玩法：

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
- 場景：`Assets/Scenes/SampleScene.unity`

### 2.2 測試裝置與環境（請填入以供重現）
- 測試裝置：Quest ________（例如 Quest 2 / Quest 3）
- OS / Runtime：________
- 光源條件：________（例如室內白光、背光強弱）
- 桌面材質：________（例如木桌/玻璃桌；反光程度）
- Room Capture 耗時：約 ________ 秒
- 測試次數 N：________（建議至少 N=10 做成功率）

---

## 3. 技術點、方法與實作對應

## 3.1 MR 平面理解（MRUK）
**做法**
- 等待 `MRUK.Instance.IsInitialized` 且 `CurrentRoom` 可用
- 以 `GenerateRandomPositionOnSurface(MRUK.SurfaceType.FACING_UP, ...)` 取樣朝上的水平面
- 使用 label bitmask 篩選：桌面常見為 `TABLE`，但實務上標記可能不穩定，因此加入 `OTHER` 容錯

**對應實作**
- `Assets/Scripts/TowerPlacer.cs`
- `Assets/Scripts/GhostSpawer.cs`（類別 `GhostSpawner`）

---

## 3.2 桌面放置（TowerPlacer：warmup + retry 抗漂移）
**問題**
MR 專案常見「初期追蹤/世界鎖定未穩時就放置」，導致物件數秒後漂移到遠處。

**策略（本專案）**
- Warmup：等待 `warmupSeconds` 後才放置
- Retry：多次取樣候選點，挑選玩家前方、距離合理、離邊距足夠的點位
- 可選 fallback：若桌面 label 不可用，可選擇退回地板（本專案預設關閉）

**SampleScene 實際參數（序列化值）**
- `warmupSeconds = 1.5`
- `autoRetries = 20`
- `retryInterval = 0.25`
- `attempts = 800`
- `targetLabels = TABLE | OTHER`（bitmask：`136`）

---

## 3.3 桌面生成（GhostSpawner：label 容錯 + 上限控制）
**策略**
- 每 `spawnTimer` 秒嘗試生成一次
- 每次生成最多嘗試 `attemptsPerSpawn` 次取樣點（避免桌面取不到點就放棄）
- `maxAlive` 限制同時存在數，避免效能發散
- `lifeTime` 讓敵人自然淘汰，避免無限累積
- 可選透明+關閉深度：提升 Passthrough 下可讀性（視材質與深度策略而定）

**SampleScene 實際參數（序列化值）**
- `spawnTimer = 1`
- `attemptsPerSpawn = 32`
- `maxAlive = 25`
- `lifeTime = 12`
- `spawnLabels = TABLE | OTHER`（bitmask：`136`）

---

## 3.4 射線槍互動（RayGun：overlap + spherecast 提升命中）
**問題**
VR/MR 射線射擊在近距離常因 collider 錯過、追蹤抖動或射線太細而 miss。

**策略（本專案）**
- 以 `closeRangeOverlapRadius` 做近距離 overlap（提升「近距離一定打得到」）
- 以 `beamRadius` 做 spherecast（提升「射線厚度」）
- 遮擋：`occlusionMask`（Tower）避免穿透不合理命中

**SampleScene 實際參數（序列化值）**
- `shootingButton = OVRInput.RawButton.RIndexTrigger`
- `beamRadius = 0.03`
- `closeRangeOverlapRadius = 0.05`
- `maxLineDistance = 5`
- `piercing = 1`
- `damagePerShot = 1`
- `layerMask = Enemy`（Layer 3，bit=8）
- `occlusionMask = Tower`（Layer 6，bit=64）

---

## 3.5 塔防規則（扣血與 Game Over 收斂）
**策略**
- `GhostEnemy` 觸塔 → `TowerHealth.TakeDamage(damage)`
- `TowerHealth` HP=0 → 觸發 `Died` event
- `GameManager` 監聽事件 → 停用 spawner/gun、清場、顯示 Game Over UI
- `RestartGame()` 由 UI Button 呼叫以重開 scene（避免狀態殘留）

---

## 4. 量測方法（Metrics）
> Spike 報告若沒有「量測與判準」，評分者無法判定你是「做出來」還是「偶然能跑」。建議至少填到可計算成功率。

### 4.1 放置成功率（Placement Success Rate）
- 定義：完成掃描後，塔在「玩家前方」且位於桌面上方（視覺上合理、數秒內不漂移到遠處）視為成功。
- 做法：重複 N 次啟動（建議 N≥10），統計成功次數。

### 4.2 生成成功率（Spawn Success Rate）
- 定義：每次 spawn interval 是否成功生成在桌面附近（不穿模、不離場景太遠）。
- 做法：固定觀察時間 T（例如 60 秒），統計生成次數與失敗次數。

### 4.3 命中率（Hit Rate）
- 近距離（<=0.5m）與中距離（0.5–2m）各射擊固定發數（例如 30 發）
- 定義：命中造成扣血/擊殺即算命中
- 目的：驗證 overlap + spherecast 策略有效

### 4.4 Game Over 收斂正確性
- 定義：塔 HP=0 時，必須同時滿足：
  1) 不再生成 Ghost（Spawner disabled）
  2) 不再接受射擊輸入（RayGun disabled）
  3) 場上 Ghost 被清除（Destroy）
  4) Game Over UI 顯示
- 做法：放任 Ghost 觸塔或調高傷害快速測試。

---

## 5. 結果（請用你們實測填入）
> 下列欄位不建議留空；若暫無數據請標註 TBD 並說明原因。

- 放置成功率：____ / ____（____%）
- 生成成功率：____ / ____（____%）
- 命中率（近距離）：____ / ____（____%）
- 命中率（中距離）：____ / ____（____%）
- Game Over 收斂：成功 / 失敗（說明：__________）
- 目視效能：穩定 / 偶發掉幀（情境：__________）

---

## 6. 已知限制與下一步
- UI：分數/波次、倒數、掃描狀態提示、Restart UX
- 難度：生成波次、速度/血量曲線、生成點避障
- MR 互動：桌面可視化提示、anchor/plane 不穩時的 fallback 設計
- 視覺：Passthrough 下材質、透明與深度策略一致化
- 量測自動化：記錄成功率與命中率至檔案（CSV）以便報告

---

## 7. 影像證據（建議）
> 若要強化可信度，建議放 3–4 段短影片到 `docs/media/`（每段 10–20 秒即可）。

- `docs/media/spike_place.mp4`（放置成功與不漂移）
- `docs/media/spike_spawn.mp4`（桌面生成）
- `docs/media/spike_shoot.mp4`（射擊命中與擊殺）
- `docs/media/spike_gameover.mp4`（扣血至 Game Over 收斂）
