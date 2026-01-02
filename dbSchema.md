# DB Schema

-   Media (미디어 정보 테이블)

    ```sql
    CREATE TABLE Media (
        MediaId TEXT PRIMARY KEY,      -- Jellyfin의 미디어 ID
        Path TEXT NOT NULL,           -- 미디어 파일 경로
        Name TEXT NOT NULL,           -- 미디어 이름
        Duration INTEGER,             -- 미디어 길이(초)
        Size BIGINT,                 -- 파일 크기(bytes)
        LastModified DATETIME,       -- 마지막 수정 시간
        LastScanned DATETIME,        -- 마지막 스캔 시간
        IsProcessed BOOLEAN,         -- 처리 완료 여부
        CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
    );
    ```

-   FrameDropCheck (프레임 드롭 검사 결과)

    ```sql
    CREATE TABLE FrameDropCheck (
        CheckId INTEGER PRIMARY KEY AUTOINCREMENT,
        MediaId TEXT NOT NULL,        -- Media 테이블 참조
        CheckStartTime DATETIME,      -- 검사 시작 시간
        CheckEndTime DATETIME,        -- 검사 종료 시간
        HasFrameDrop BOOLEAN,        -- 프레임 드롭 발생 여부
        FrameDropCount INTEGER,      -- 프레임 드롭 발생 횟수
        LogAnalysisResult TEXT,      -- 로그 분석 결과
        PlaybackAnalysisResult TEXT, -- 재생 분석 결과
        Status TEXT,                 -- 검사 상태 (pending/in-progress/completed/failed)
        FOREIGN KEY (MediaId) REFERENCES Media(MediaId)
    );
    ```

-   FrameDropDetail (프레임 드롭 상세 정보)

    ```sql
    CREATE TABLE FrameDropDetail (
        DetailId INTEGER PRIMARY KEY AUTOINCREMENT,
        CheckId INTEGER NOT NULL,     -- FrameDropCheck 참조
        Timestamp DATETIME,          -- 프레임 드롭 발생 시간
        DropType TEXT,              -- 드롭 유형 (log/playback)
        TimeOffset INTEGER,         -- 발생 지점(초)
        Description TEXT,           -- 상세 설명
        FOREIGN KEY (CheckId) REFERENCES FrameDropCheck(CheckId)
    );
    ```

-   EncodingJob (인코딩 작업 정보)

    ```sql
    CREATE TABLE EncodingJob (
        JobId INTEGER PRIMARY KEY AUTOINCREMENT,
        MediaId TEXT NOT NULL,        -- Media 테이블 참조
        OriginalPath TEXT NOT NULL,   -- 원본 파일 경로
        BackupPath TEXT,             -- 백업 파일 경로
        NewFilePath TEXT,            -- 새 파일 경로
        StartTime DATETIME,          -- 작업 시작 시간
        EndTime DATETIME,            -- 작업 종료 시간
        Status TEXT,                 -- 작업 상태 (pending/in-progress/completed/failed)
        ErrorMessage TEXT,           -- 오류 메시지
        FOREIGN KEY (MediaId) REFERENCES Media(MediaId)
    );
    ```

-   BackupCleanupLog (백업 파일 정리 로그)

    ```sql
    CREATE TABLE BackupCleanupLog (
        CleanupId INTEGER PRIMARY KEY AUTOINCREMENT,
        MediaId TEXT NOT NULL,        -- Media 테이블 참조
        BackupPath TEXT NOT NULL,    -- 삭제된 백업 파일 경로
        CleanupTime DATETIME,        -- 정리 시간
        FOREIGN KEY (MediaId) REFERENCES Media(MediaId)
    );
    ```
