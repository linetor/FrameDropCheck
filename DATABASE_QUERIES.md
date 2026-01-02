# FrameDropCheck Database Queries

이 문서는 FrameDropCheck 플러그인의 SQLite 데이터베이스를 직접 쿼리하여 디버깅하는 방법을 설명합니다.

## Database 위치

서버에서 DB 파일 경로:

```
/path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db
```

## 서버 접속

```bash
ssh user@your-jellyfin-server
```

## 기본 쿼리 명령

### 1. 특정 미디어 정보 조회

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT MediaId, Name, AverageDropRate, OptimizationStatus, LastScanned 
FROM Media 
WHERE Name LIKE '%검색어%'"
```

**예시: CAWD-621 조회**

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT MediaId, Name, AverageDropRate, OptimizationStatus, LastScanned 
FROM Media 
WHERE Name LIKE '%CAWD-621%'"
```

**출력 예시:**

```
a96e4c17-d182-6247-bc34-03e263df1a33|CAWD-621 불륜...|0.0|Healthy|2026-01-01 12:26:00
```

### 2. 클라이언트 재생 통계 조회 (드롭률 계산 포함)

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT 
    MediaId,
    FramesDropped,
    PlaybackDuration,
    ROUND((CAST(FramesDropped AS FLOAT) / (PlaybackDuration * 24) * 100), 2) as DropRate,
    Timestamp
FROM ClientPlaybackStats 
WHERE MediaId LIKE '%검색어%' 
ORDER BY Timestamp"
```

**예시: CAWD-621의 모든 재생 기록**

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT 
    MediaId,
    FramesDropped,
    PlaybackDuration,
    ROUND((CAST(FramesDropped AS FLOAT) / (PlaybackDuration * 24) * 100), 2) as DropRate,
    Timestamp
FROM ClientPlaybackStats 
WHERE MediaId LIKE '%a96e4c17%' 
ORDER BY Timestamp"
```

**출력 예시:**

```
a96e4c17d1826247bc3403e263df1a33|0|0.0||2026-01-01 05:44:14
a96e4c17d1826247bc3403e263df1a33|2|5.0|1.67|2026-01-01 05:44:19
a96e4c17d1826247bc3403e263df1a33|5|10.0|2.08|2026-01-01 05:44:24
a96e4c17d1826247bc3403e263df1a33|8|15.0|2.22|2026-01-01 05:44:29
a96e4c17d1826247bc3403e263df1a33|9|60.0|0.63|2026-01-01 05:45:55
```

### 3. 10초 이상 재생 중 임계값 초과 확인

**OptimizationAnalyzer 로직과 동일한 쿼리**

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT 
    MediaId,
    FramesDropped,
    PlaybackDuration,
    ROUND((CAST(FramesDropped AS FLOAT) / (PlaybackDuration * 24) * 100), 2) as DropRate,
    CASE 
        WHEN PlaybackDuration >= 10 
             AND (CAST(FramesDropped AS FLOAT) / (PlaybackDuration * 24) * 100) > 0.1 
        THEN 'BAD' 
        ELSE 'OK' 
    END as Status
FROM ClientPlaybackStats 
WHERE MediaId LIKE '%a96e4c17%'
ORDER BY Timestamp"
```

**출력 예시:**

```
a96e4c17d1826247bc3403e263df1a33|0|0.0||OK
a96e4c17d1826247bc3403e263df1a33|2|5.0|1.67|OK (10초 미만)
a96e4c17d1826247bc3403e263df1a33|5|10.0|2.08|BAD
a96e4c17d1826247bc3403e263df1a33|8|15.0|2.22|BAD
a96e4c17d1826247bc3403e263df1a33|9|60.0|0.63|BAD
```

### 4. badClientReports 계산 (인코딩 판단 기준)

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT 
    COUNT(*) as badClientReports
FROM ClientPlaybackStats 
WHERE MediaId LIKE '%a96e4c17%'
  AND PlaybackDuration >= 10
  AND (CAST(FramesDropped AS FLOAT) / (PlaybackDuration * 24) * 100) > 0.1"
```

**출력 예시:**

```
3
```

**해석:**

- `badClientReports = 3`
- 임계값: `>= 2`
- **결과: clientSideIssue = true → 인코딩 필요!**

### 5. 전체 미디어 개수 확인

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT COUNT(*) FROM Media"
```

### 6. 상태별 미디어 개수

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT 
    OptimizationStatus, 
    COUNT(*) as Count 
FROM Media 
GROUP BY OptimizationStatus"
```

**출력 예시:**

```
Healthy|85
Pending|23
Optimized|3
```

### 7. 인코딩 대상 미디어 조회

서버 드롭률 또는 클라이언트 재생 문제가 있는 미디어:

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db \
"SELECT 
    M.MediaId,
    M.Name,
    M.AverageDropRate as ServerDropRate,
    M.OptimizationStatus,
    (SELECT COUNT(*) 
     FROM ClientPlaybackStats C 
     WHERE C.MediaId = M.MediaId 
       AND C.PlaybackDuration >= 10
       AND (CAST(C.FramesDropped AS FLOAT) / (C.PlaybackDuration * 24) * 100) > 0.1
    ) as BadClientReports
FROM Media M
WHERE M.AverageDropRate > 0.1 
   OR (SELECT COUNT(*) 
       FROM ClientPlaybackStats C 
       WHERE C.MediaId = M.MediaId 
         AND C.PlaybackDuration >= 10
         AND (CAST(C.FramesDropped AS FLOAT) / (C.PlaybackDuration * 24) * 100) > 0.1
      ) >= 2
ORDER BY BadClientReports DESC, ServerDropRate DESC"
```

### 8. 특정 미디어의 상세 분석

```bash
sqlite3 /path/to/jellyfin/config/data/plugins/FrameDropCheck/framedrop.db << EOF
.mode column
.headers on
SELECT 
    'Media Info' as Category,
    M.Name as Detail,
    CAST(M.AverageDropRate AS TEXT) as Value
FROM Media M
WHERE M.MediaId LIKE '%a96e4c17%'

UNION ALL

SELECT 
    'Client Stats' as Category,
    'Playback #' || ROW_NUMBER() OVER (ORDER BY C.Timestamp) as Detail,
    'Duration: ' || C.PlaybackDuration || 's, Drops: ' || C.FramesDropped || 
    ', Rate: ' || ROUND((CAST(C.FramesDropped AS FLOAT) / (C.PlaybackDuration * 24) * 100), 2) || '%' as Value
FROM ClientPlaybackStats C
WHERE C.MediaId LIKE '%a96e4c17%'
ORDER BY Category, Detail;
EOF
```

## OptimizationAnalyzer 로직 요약

현재 설정된 판단 기준:

### 서버 측 (Probe)

```
serverSideIssue = media.AverageDropRate > 0.1%
```

### 클라이언트 측 (재생 통계)

```
badClientReports = COUNT(
    재생 시간 >= 10초 AND
    드롭률 > 0.1%
)

clientSideIssue = badClientReports >= 2
```

### 최종 판단

```
if (serverSideIssue OR clientSideIssue):
    Action = ReEncode (인코딩 필요)
else:
    Action = None (정상)
```

## 문제 해결 예시

### Case: CAWD-621이 Healthy인데 인코딩되어야 할 것 같음

1. **미디어 기본 정보 확인**

```bash
sqlite3 .../framedrop.db "SELECT * FROM Media WHERE Name LIKE '%CAWD-621%'"
```

1. **클라이언트 재생 기록 확인**

```bash
sqlite3 .../framedrop.db "SELECT ... FROM ClientPlaybackStats WHERE MediaId LIKE '%a96e4c17%'"
```

1. **badClientReports 계산**

```bash
sqlite3 .../framedrop.db "SELECT COUNT(*) FROM ClientPlaybackStats WHERE ... >= 10 AND ... > 0.1"
```

1. **결과 해석**
   - badClientReports >= 2 → 인코딩 필요
   - badClientReports < 2 → 정상 (false alarm 가능성)

## 주의사항

- MediaId는 하이픈(`-`) 또는 없이 저장될 수 있습니다
  - `a96e4c17-d182-6247-bc34-03e263df1a33` (하이픈 있음)
  - `a96e4c17d1826247bc3403e263df1a33` (하이픈 없음)
- LIKE 검색 시 `%일부문자열%` 사용
- 드롭률 계산 시 24fps 가정 (설정 가능)
- 임계값(DropThreshold)은 플러그인 설정에서 변경 가능 (기본 0.1%)
