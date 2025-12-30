USE master;
GO

IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'GeoErosionDB')
BEGIN
    CREATE DATABASE GeoErosionDB;
END
GO

USE GeoErosionDB;
GO

IF OBJECT_ID('ErosionLog', 'U') IS NOT NULL
    DROP TABLE ErosionLog;
GO

CREATE TABLE ErosionLog(
    Step        int PRIMARY KEY,        -- 第几万年的步
    Rain        real,                   -- 当前雨量 P
    ErodeK      real,                   -- 侵蚀系数 K
    DepositD    real,                   -- 沉积系数 D
    ThresholdT  real,                   -- 侵蚀阈值 T
    UpliftU     real,                   -- 抬升速率 U
    MaxRelief   real,                   -- 最大高差(m)
    MeanElev    real,                   -- 平均高程(m)
    DrainDen    real,                   -- 沟壑密度
    HackSlope   real,                   -- logL~logA 斜率
    Concavity   real                    -- logS~logA 斜率
);
GO
