CREATE OR REPLACE PACKAGE BODY PKG_PP009_LTS004 AS
    /*****************************************************************************
    * 프로그램명 : 사업계획 데이터 복사(모델선 및 시뮬레이션 전용)
    *-----------------------------------------------------------------------------
    * 작  성  자 : 홍석민
    * 작  성  일 : 2021-07-09
    * 설      명 : 사업계획 데이터 복사
    *******************************************************************************
       VER        DATE         AUTHOR           DESCRIPTION
       ---------  ----------  ---------------  ------------------------------------
       1.0        2021-07-09                    1. CREATED THIS PACKAGE.
    ******************************************************************************/

    PROCEDURE TSAC003_FIG_NO_LIST
    (
        I_PRJ_GBN IN VARCHAR2,
        O_CUR  OUT SYS_REFCURSOR
    )
    IS

    BEGIN
        IF I_PRJ_GBN = 'E000' THEN
            OPEN O_CUR FOR
            SELECT FIG_NO, IN_DAT
            FROM TSAC003
            WHERE PRJ_GBN= I_PRJ_GBN
                AND FIG_NO NOT LIKE 'SCENA-%'
                AND FIG_NO NOT LIKE 'MODEL%'
            ORDER BY IN_DAT DESC;
        ELSIF I_PRJ_GBN = 'E000_MODEL' THEN
            OPEN O_CUR FOR
            SELECT FIG_NO, IN_DAT
            FROM TSAC003
            WHERE PRJ_GBN= I_PRJ_GBN
                AND FIG_NO NOT LIKE 'SCENA-%'
                AND FIG_NO LIKE 'MODEL%'
            ORDER BY IN_DAT DESC;
        ELSIF I_PRJ_GBN LIKE '%MODEL' THEN
            OPEN O_CUR FOR
            SELECT FIG_NO, IN_DAT
            FROM TSAC003
            WHERE PRJ_GBN='C000'
                AND CRE_GBN = '2'
                AND FIG_NO NOT LIKE 'SCENA-%'
                AND FIG_NO LIKE 'MODEL%'
            ORDER BY IN_DAT DESC;
        ELSE
            OPEN O_CUR FOR
            SELECT FIG_NO, IN_DAT
            FROM TSAC003
            WHERE PRJ_GBN='C000'
--                AND CRE_GBN = '2'
                AND FIG_NO NOT LIKE 'SCENA-%'
                AND FIG_NO NOT LIKE 'MODEL%'
            ORDER BY IN_DAT DESC;
        END IF;

    END TSAC003_FIG_NO_LIST;
--------------------------------------------------------------------------------


/******************************************************************************
   - 최종수정일 : 2020-02-09
   - 최종수정자 :
   - 기능 상세 : 도크 배치 선표 번호 조회 - Combo
******************************************************************************/
--------------------------------------------------------------------------------
    PROCEDURE TSAC003_NEWEST_FIG_NO_LIST
--------------------------------------------------------------------------------
    (
        I_PRJ_GBN IN VARCHAR2,
        O_CUR  OUT SYS_REFCURSOR
    )
    IS

    BEGIN

      OPEN O_CUR FOR
            SELECT DISTINCT TA1.FIG_NO, TB1.FIG_DES, TB1.IN_DAT
          FROM TSAF100 TA1, TSAC003 TB1
         WHERE TA1.FIG_NO = TB1.FIG_NO
           AND TB1.SFIG_YN = '1' --생산선표발생
           AND TB1.PLN_YN = '1' -- 확정
              --AND TB1.YEA_PNT_YN = '1' -- 년간절점일정 계획
           AND (TB1.PRJ_GBN IS NULL OR TB1.PRJ_GBN = 'C000')
         UNION
        SELECT DISTINCT TA1.FIG_NO, TB1.FIG_DES, TB1.IN_DAT
          FROM TSAF100 TA1, TSAC003 TB1
         WHERE TA1.FIG_NO = TB1.FIG_NO
           AND TB1.CRE_GBN IN ('0')
           AND TB1.PLN_YN = '1'
              --AND TB1.YEA_PNT_YN = '1'
           AND (TB1.PRJ_GBN IS NULL OR TB1.PRJ_GBN = 'C000')
         ORDER BY IN_DAT DESC;

        /*
        IF I_PRJ_GBN = 'E000' THEN
            OPEN O_CUR FOR
            SELECT FIG_NO, FIG_DES,  IN_DAT
            FROM TSAC003 B
            WHERE B.PLN_YN = '1'
                AND (B.CRE_GBN='0' OR B.SFIG_YN = '1')
                AND (B.PRJ_GBN IS NULL OR B.PRJ_GBN = I_PRJ_GBN)
            UNION
            SELECT FIG_NO, FIG_DES, IN_DAT
            FROM TSAC003
            WHERE PRJ_GBN= I_PRJ_GBN
                AND FIG_NO NOT LIKE 'SCENA-%'
                AND FIG_NO NOT LIKE 'MODEL%'
            ORDER BY IN_DAT DESC;
        ELSE
            OPEN O_CUR FOR
            SELECT FIG_NO, FIG_DES,  IN_DAT
            FROM TSAC003 B
            WHERE B.PLN_YN = '1'
                AND (B.CRE_GBN='0' OR B.SFIG_YN = '1')
                AND (B.PRJ_GBN IS NULL OR B.PRJ_GBN = 'C000')
            UNION
            SELECT FIG_NO, FIG_DES, IN_DAT
            FROM TSAC003
            WHERE PRJ_GBN='C000'
                AND CRE_GBN = '2'
                AND FIG_NO NOT LIKE 'SCENA-%'
                AND FIG_NO NOT LIKE 'MODEL%'
            ORDER BY IN_DAT DESC;
        END IF;
        */

    END TSAC003_NEWEST_FIG_NO_LIST;
--------------------------------------------------------------------------------


/******************************************************************************
   - 최종수정일 : 2020-02-10
   - 최종수정자 :
   - 기능 상세 : 선표기준호선 조회
******************************************************************************/
--------------------------------------------------------------------------------
    PROCEDURE TSAD001_KL_SELECT
--------------------------------------------------------------------------------
    (
        I_FIG_NO      IN VARCHAR2,
        I_START_DATE  IN VARCHAR2,
        I_FINISH_DATE IN VARCHAR2,
        I_DOCK_FIG_NO IN VARCHAR2,
        O_CUR  OUT SYS_REFCURSOR
    )
    IS

    BEGIN
        OPEN O_CUR FOR
        WITH ACT_INFO AS
        (
          SELECT /*+ NO_MERGE */TRIM(FIG_NO)FIG_NO, SHP_COD, SUM(MUL_WGT) HUL_MUL_WGT, SUM(BLK_ACT_CNT) BLK_ACT_CNT, SUM(AREA_ACT_CNT) AREA_ACT_CNT, SUM(SHIP_MHR) SHIP_MHR
               , SUM(MUL_4B) MUL_4B, SUM(MUL_32) MUL_32, SUM(MUL_52) MUL_52, SUM(MUL_53) MUL_53, SUM(MUL_54) MUL_54
            FROM (
                  SELECT /*+ INDEX(INDEX_FIG_PP009_LTS004) */Z1.FIG_NO, Z1.SHP_COD
                       , SUM(CASE WHEN WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X' THEN MUL_WGT ELSE 0 END) MUL_WGT
                       , 0 BLK_ACT_CNT, 0 AREA_ACT_CNT
                       , 0 SHIP_MHR --공수열 조회 컬럼 제거
--                       , SUM(CASE WHEN NVL(Z1.EXP_MHR, 0) > 0 THEN Z1.EXP_MHR
--                                  WHEN NVL(Z1.MHR_REL, 0) > 0 THEN Z1.MHR_REL
--                                  ELSE Z1.STD_MHR END) SHIP_MHR   --추정 -> 실 -> 표준
                       , SUM(CASE WHEN Z1.WRK_TYP = '4B' AND Z2.MUL_UNIT = 'H01'
                                  THEN Z1.MUL_WGT_O_1
                                  ELSE 0 END) MUL_4B
                       , SUM(CASE WHEN Z1.WRK_TYP = '32' AND Z2.MUL_UNIT = 'M21'
                                  THEN Z1.MUL_WGT_O_1
                                  ELSE 0 END) MUL_32
                       , SUM(CASE WHEN Z1.WRK_TYP = '52' AND Z2.MUL_UNIT = 'PC1'
                                  THEN Z1.MUL_WGT_O_1
                                  ELSE 0 END) MUL_52
                       , SUM(CASE WHEN Z1.WRK_TYP = '53' AND Z2.MUL_UNIT = 'PC1'
                                  THEN Z1.MUL_WGT_O_1
                                  ELSE 0 END) MUL_53
                       , SUM(CASE WHEN Z1.WRK_TYP = '54' AND Z2.MUL_UNIT = 'E01'
                                  THEN Z1.MUL_WGT_O_1
                                  ELSE 0 END) MUL_54
                    FROM TSEG005 Z1,
                         TSMG033 Z2
                   WHERE Z1.FIG_NO = RPAD(I_FIG_NO, 9, ' ')
                     AND SUBSTR(Z1.POS_ID,5,5) = Z2.WBS_ID(+)
                   GROUP BY Z1.FIG_NO, Z1.SHP_COD
                   UNION ALL
                  SELECT /*+ INDEX(INDEX_TSMG_TSFA001_LTS004) */Z1.FIG_NO, Z1.SHP_COD, 0 MUL_WGT, COUNT(*) BLK_ACT_CNT, 0 AREA_ACT_CNT
                       , 0 SHIP_MHR --공수열 조회 컬럼 제거
--                       , SUM(CASE WHEN NVL(Z1.EXP_MHR, 0) > 0 THEN Z1.EXP_MHR
--                                  WHEN NVL(Z1.MHR_STU, 0) > 0 THEN Z1.MHR_STU
--                                  ELSE Z1.STD_MHR END) SHIP_MHR
                       , SUM(CASE WHEN Z1.WRK_TYP = '4B' AND Z2.MUL_UNIT = 'H01'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_4B
                       , SUM(CASE WHEN Z1.WRK_TYP = '32' AND Z2.MUL_UNIT = 'M21'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_32
                       , SUM(CASE WHEN Z1.WRK_TYP = '52' AND Z2.MUL_UNIT = 'PC1'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_52
                       , SUM(CASE WHEN Z1.WRK_TYP = '53' AND Z2.MUL_UNIT = 'PC1'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_53
                       , SUM(CASE WHEN Z1.WRK_TYP = '54' AND Z2.MUL_UNIT = 'E01'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_54
                    FROM TSMG_TSFA001 Z1,
                         TSMG033 Z2
                   WHERE Z1.FIG_NO = RPAD(I_FIG_NO, 9, ' ')
                     AND SUBSTR(Z1.POS_ID,5,5) = Z2.WBS_ID(+)
                   GROUP BY Z1.FIG_NO, Z1.SHP_COD
                   UNION ALL
                  SELECT /*+ INDEX(INDEX_TSMG_TSFN101_LTS004) */Z1.FIG_NO, Z1.SHP_COD, 0 MUL_WGT, 0 BLK_ACT_CNT, COUNT(*) AREA_ACT_CNT
                       , 0 SHIP_MHR --공수열 조회 컬럼 제거
--                       , SUM(CASE WHEN NVL(Z1.EXP_MHR, 0) > 0 THEN Z1.EXP_MHR
--                                  WHEN NVL(Z1.MHR_STU, 0) > 0 THEN Z1.MHR_STU
--                                  ELSE Z1.STD_MHR END) SHIP_MHR
                       , SUM(CASE WHEN Z1.WRK_TYP = '4B' AND Z2.MUL_UNIT = 'H01'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_4B
                       , SUM(CASE WHEN Z1.WRK_TYP = '32' AND Z2.MUL_UNIT = 'M21'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_32
                       , SUM(CASE WHEN Z1.WRK_TYP = '52' AND Z2.MUL_UNIT = 'PC1'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_52
                       , SUM(CASE WHEN Z1.WRK_TYP = '53' AND Z2.MUL_UNIT = 'PC1'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_53
                       , SUM(CASE WHEN Z1.WRK_TYP = '54' AND Z2.MUL_UNIT = 'E01'
                                  THEN Z1.MUL_QTY
                                  ELSE 0 END) MUL_54
                    FROM TSMG_TSFN101 Z1,
                         TSMG033 Z2
                   WHERE Z1.FIG_NO = RPAD(I_FIG_NO, 9, ' ')
                     AND SUBSTR(Z1.POS_ID,5,5) = Z2.WBS_ID(+)
                   GROUP BY Z1.FIG_NO, Z1.SHP_COD
                  )
            GROUP BY FIG_NO, SHP_COD
        ),
        TB_RATE AS
        (
            SELECT CC.FIG_SHP, CASE WHEN CC.LOA <> 0 AND AA.BLD_LEN='0' THEN 0
                                    WHEN CC.LOA <> 0 AND AA.BLD_LEN <> '0' THEN ROUND((AA.BLD_LEN*100)/CC.LOA,1)
                                    ELSE 0 END AS BLD_RATE, AA.*
            FROM TSAF010 AA, TSAD001 CC
            WHERE AA.FIG_NO    = I_DOCK_FIG_NO
                AND AA.FIG_NO  = CC.FIG_NO
                AND AA.SHP_COD = CC.SHP_COD
                AND AA.BCH_KND = '2' 
                AND AA.FT_GBN='0'
                AND AA.DCK_COD = CC.DCK_COD
        ),
        TB_FIG9 AS
        (
            SELECT * FROM TSEG044 WHERE FIG_NO ='999999999'
        )
        SELECT
            B.A_SHP_DES, B.TANDEM, B.FIG_DES, A.SHP_COD, A.FIG_NO, A.STD_SHP, A.MAK_TM
                , A.FIG_SHP AS CPY_FIG_SHP,  A.SHP_TYP_NM,  A.SHP_TYP_QTY,  A.LOA,  A.LBP,  A.WID
                , A.DEP, A.DRF_DSN, A.DRF_SCT, A.DWT_DSN, A.DWT_SCT, A.FCV_LEN, A.ACV_LEN
                , A.AFT_WID, A.TOT_TON, A.CG_TON, A.NHSW, A.SHP_VAL, A.STL_VAL, A.STL_TON
                , A.MAT_VAL, A.MHR_VAL, A.MHR_TOT, A.WST_VAL, A.ETC_VAL, A.BLD_COD
                , A.ASM_ND, A.ASM_NW, A.DCK_ND, A.DCK_NW, A.QUAY_ND, A.QUAY_NW
                , A.DCK_COD, A.WC, A.KL, A.FT, A.LC, A.DL, A.DL_YU, A.DL_GY, A.DL_NET
                , A.CNT_STS, A.SHA_PNT, A.SHA_TYP, A.SHP_GBN, A.PNT_YN, A.SHP_DES
                , A.MST_YN, A.IN_DAT, A.UP_DAT, A.IN_USR, A.UP_USR, A.SHP_KND
                , A.WCT, A.WCH, A.STD_YEA, A.BCH_NO, A.SHP_TYP, A.DL_SJ, A.DL_GR
                , A.ASM_CD, A.ASM_CW, A.ASM_CM, A.DCK_CD, A.DCK_CW, A.DCK_CM
                , A.QUAY_CD, A.QUAY_CW, A.QUAY_CM, A.BNP_GBN, A.OWNRP_NM
                , A.NW_GEN_FLG, A.MAK_SSTM, A.MAK_MSTM, A.MAK_LSTM, A.PMIX_YN, A.FIG_OWN_NM
                , A.PRJ_GBN, A.REF_SHP, A.SHP_TYP1, A.ADD_QUAY_TERM, A.MHR_PRO
                , A.AF, A.FL, A.REF_FIGNO, A.KL2, A.FT2, A.FT3
                , A.KL_DCK, A.KL2_DCK, A.FT_DCK, A.FT2_DCK, A.FT3_DCK, A.NOTE, A.SC_ID
--                , (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.FIG_NO AND SHP_COD = A.SHP_COD AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X') SUM_MUL_WGT
--                , CASE WHEN A.CPY_FIG_NO != '진행' AND A.CPY_FIG_NO != '999999999' THEN
--                            (SELECT /*+ NO_MERGE */SUM(Z3.MUL_WGT) FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD)
--                       WHEN A.CPY_FIG_NO IS NOT NULL THEN
--                            (SELECT SUM(MUL_WGT) FROM TSEA002 WHERE FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X')
--                       ELSE 0
--                    END SUM_MUL_WGT2
                , 0 SUM_MUL_WGT2
                , A.ORI_TON, A.EX_RATE_PRO, A.PROFIT_PRO, A.NOTE_INQ, A.NOTE_CDS
                , C.KIND, AA.BLD_COD AS DCK_BLD_COD
                , CASE WHEN A.LOA <> 0 AND AA.BLD_LEN='0' THEN 0
                       WHEN A.LOA <> 0 AND AA.BLD_LEN <> '0' THEN ROUND((AA.BLD_LEN*100)/A.LOA,1)
                       ELSE 0 END AS BLD_RATE
               , B.A_SHP_DES_OFT                 -- 의장-모델코드
               , B.FIG_DES_OFT                   -- 의장-설명
               , CASE WHEN I_FIG_NO LIKE 'MODEL%' THEN B.A_SHP_DES ELSE A.FIG_SHP END FIG_SHP_DES, A.FIG_SHP
               , CASE WHEN A.FIG_SHP LIKE 'U%' THEN 'N'
                      WHEN C.KIND IS NULL THEN 'N'
                      WHEN C.KIND IN ('P', 'E') THEN 'Y'
                      ELSE 'N' END RUN_SHIP_INDC
               , CASE WHEN C.KIND IN ('P', 'E') AND A.CPY_SHP IS NULL
                      THEN A.FIG_SHP
                      ELSE A.CPY_SHP END CPY_SHP            -- 선각-모델호선
               , CASE WHEN C.KIND IN ('P', 'E') AND A.CPY_FIG_NO IS NULL
                      THEN '999999999'
                      ELSE A.CPY_FIG_NO END CPY_FIG_NO      -- 선각-모델선표
               , CASE WHEN C.KIND IN ('P', 'E') AND A.CPY_SHP1 IS NULL
                      THEN A.FIG_SHP
                      ELSE A.CPY_SHP1 END CPY_FIG_SHP_OFT   -- 의장-모델호선
               , CASE WHEN C.KIND IN ('P', 'E') AND A.CPY_FIG_NO1 IS NULL
                      THEN '999999999'
                      ELSE A.CPY_FIG_NO1 END CPY_FIG_NO_OFT -- 의장-모델선표
               , D.HUL_MUL_WGT
               , D.HUL_MUL_WGT AS SUM_MUL_WGT
               , D.BLK_ACT_CNT
               , D.AREA_ACT_CNT
               , D.SHIP_MHR
               , D.MUL_4B
               , D.MUL_32
               , D.MUL_52
               , D.MUL_53
               , D.MUL_54
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_WGT)      FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) SUM_MUL_WGT
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_WGT)      FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) HUL_MUL_WGT
--               , (SELECT /*+ NO_MERGE */SUM(Z3.BLK_ACT_CNT)  FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) BLK_ACT_CNT
--               , (SELECT /*+ NO_MERGE */SUM(Z3.AREA_ACT_CNT) FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) AREA_ACT_CNT
--               , (SELECT /*+ NO_MERGE */SUM(Z3.SHIP_MHR)     FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) SHIP_MHR
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_4B)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_4B
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_32)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_32
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_52)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_52
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_53)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_53
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_54)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_54
            FROM TSAD001 A, TSEO151 B, TB_FIG9 C, TB_RATE AA
                , ACT_INFO D
            WHERE A.FIG_NO = B.FIG_NO(+)
                AND A.FIG_SHP = B.FIG_SHP(+)
                AND A.FIG_SHP = C.FIG_SHP(+)
--                AND A.FIG_NO  = AA.FIG_NO(+)
                AND A.SHP_COD = AA.SHP_COD(+)
--                AND A.DCK_COD = AA.DCK_COD(+)
                AND A.FIG_SHP NOT LIKE '%A'
                AND A.FIG_NO = I_FIG_NO  AND I_START_DATE <= A.DL AND I_FINISH_DATE >= A.WC
                AND A.DCK_COD IN ('1','2','3','4','5','8','9','G','H')
                AND A.FIG_NO = D.FIG_NO(+)
                AND A.SHP_COD = D.SHP_COD(+)
            UNION ALL
            SELECT
                    B.A_SHP_DES, B.TANDEM, B.FIG_DES, A.SHP_COD, A.FIG_NO, A.STD_SHP, A.MAK_TM
                    , A.FIG_SHP AS CPY_FIG_SHP,  A.SHP_TYP_NM,  A.SHP_TYP_QTY,  A.LOA,  A.LBP,  A.WID
                    , A.DEP, A.DRF_DSN, A.DRF_SCT, A.DWT_DSN, A.DWT_SCT, A.FCV_LEN, A.ACV_LEN
                    , A.AFT_WID, A.TOT_TON, A.CG_TON, A.NHSW, A.SHP_VAL, A.STL_VAL, A.STL_TON
                    , A.MAT_VAL, A.MHR_VAL, A.MHR_TOT, A.WST_VAL, A.ETC_VAL, A.BLD_COD
                    , A.ASM_ND, A.ASM_NW, A.DCK_ND, A.DCK_NW, A.QUAY_ND, A.QUAY_NW
                    , A.DCK_COD, A.WC, A.KL, A.FT, A.LC, A.DL, A.DL_YU, A.DL_GY, A.DL_NET
                    , A.CNT_STS, A.SHA_PNT, A.SHA_TYP, A.SHP_GBN, A.PNT_YN, A.SHP_DES
                    , A.MST_YN, A.IN_DAT, A.UP_DAT, A.IN_USR, A.UP_USR, A.SHP_KND
                    , A.WCT, A.WCH, A.STD_YEA, A.BCH_NO, A.SHP_TYP, A.DL_SJ, A.DL_GR
                    , A.ASM_CD, A.ASM_CW, A.ASM_CM, A.DCK_CD, A.DCK_CW, A.DCK_CM
                    , A.QUAY_CD, A.QUAY_CW, A.QUAY_CM, A.BNP_GBN, A.OWNRP_NM
                    , A.NW_GEN_FLG, A.MAK_SSTM, A.MAK_MSTM, A.MAK_LSTM, A.PMIX_YN, A.FIG_OWN_NM
                    , A.PRJ_GBN, A.REF_SHP, A.SHP_TYP1, A.ADD_QUAY_TERM, A.MHR_PRO
                    , A.AF, A.FL, A.REF_FIGNO, A.KL2, A.FT2, A.FT3
                    , A.KL_DCK, A.KL2_DCK, A.FT_DCK, A.FT2_DCK, A.FT3_DCK, A.NOTE, A.SC_ID
--                    , CASE WHEN A.CPY_FIG_NO != '진행' AND A.CPY_FIG_NO != '999999999' THEN
--                                (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.CPY_FIG_NO AND FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X')
--                           WHEN A.CPY_FIG_NO IS NOT NULL THEN
--                                (SELECT SUM(MUL_WGT) FROM TSEA002 WHERE FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X')
--                           ELSE 0
--                       END SUM_MUL_WGT2
                    , 0 SUM_MUL_WGT2
                    , A.ORI_TON, A.EX_RATE_PRO, A.PROFIT_PRO, A.NOTE_INQ, A.NOTE_CDS
                    , C.KIND, AA.BLD_COD AS DCK_BLD_COD
                    , CASE WHEN A.LOA <> 0 AND AA.BLD_LEN='0' THEN 0
                           WHEN A.LOA <> 0 AND AA.BLD_LEN <> '0' THEN ROUND((AA.BLD_LEN*100)/A.LOA,1)
                           ELSE 0 END AS BLD_RATE
                   , B.A_SHP_DES_OFT                 -- 의장-모델코드
                   , B.FIG_DES_OFT                   -- 의장-설명
                   , CASE WHEN I_FIG_NO LIKE 'MODEL%' THEN B.A_SHP_DES ELSE A.FIG_SHP END FIG_SHP_DES, A.FIG_SHP
                   , CASE WHEN A.FIG_SHP LIKE 'U%' THEN 'N'
                          WHEN C.KIND IS NULL THEN 'N'
                          WHEN C.KIND IN ('P', 'E') THEN 'Y'
                          ELSE 'N' END RUN_SHIP_INDC
                   , CASE WHEN C.KIND IN ('P', 'E') AND A.CPY_SHP IS NULL
                              THEN A.FIG_SHP
                              ELSE A.CPY_SHP END CPY_SHP            -- 선각-모델호선
                       , CASE WHEN C.KIND IN ('P', 'E') AND A.CPY_FIG_NO IS NULL
                              THEN '999999999'
                              ELSE A.CPY_FIG_NO END CPY_FIG_NO      -- 선각-모델선표
                       , CASE WHEN C.KIND IN ('P', 'E') AND A.CPY_SHP1 IS NULL
                              THEN A.FIG_SHP
                              ELSE A.CPY_SHP1 END CPY_FIG_SHP_OFT   -- 의장-모델호선
                       , CASE WHEN C.KIND IN ('P', 'E') AND A.CPY_FIG_NO1 IS NULL
                              THEN '999999999'
                              ELSE A.CPY_FIG_NO1 END CPY_FIG_NO_OFT -- 의장-모델선표
                       , D.HUL_MUL_WGT
                       , D.HUL_MUL_WGT AS SUM_MUL_WGT
                       , D.BLK_ACT_CNT
                       , D.AREA_ACT_CNT
                       , D.SHIP_MHR
                       , D.MUL_4B
                       , D.MUL_32
                       , D.MUL_52
                       , D.MUL_53
                       , D.MUL_54
--               , (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.FIG_NO AND SHP_COD = A.SHP_COD AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X') SUM_MUL_WGT
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_WGT)      FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) HUL_MUL_WGT
--               , (SELECT /*+ NO_MERGE */SUM(Z3.BLK_ACT_CNT)  FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) BLK_ACT_CNT
--               , (SELECT /*+ NO_MERGE */SUM(Z3.AREA_ACT_CNT) FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) AREA_ACT_CNT
--               , (SELECT /*+ NO_MERGE */SUM(Z3.SHIP_MHR)     FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) SHIP_MHR
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_4B)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_4B
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_32)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_32
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_52)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_52
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_53)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_53
--               , (SELECT /*+ NO_MERGE */SUM(Z3.MUL_54)       FROM ACT_INFO Z3 WHERE A.FIG_NO = Z3.FIG_NO AND A.SHP_COD = Z3.SHP_COD) MUL_54
            FROM TSAD001 A, TSEO151 B, TB_FIG9 C, TB_RATE AA
                 , ACT_INFO D
            WHERE A.FIG_NO = B.FIG_NO(+)
                AND A.FIG_SHP = B.FIG_SHP(+)
                AND A.FIG_SHP = C.FIG_SHP(+)
--                AND A.FIG_NO  = AA.FIG_NO(+)
                AND A.SHP_COD = AA.SHP_COD(+)
--                AND A.DCK_COD = AA.DCK_COD(+)
                AND A.FIG_SHP NOT LIKE '%A'
                AND A.FIG_NO = I_FIG_NO  AND I_START_DATE <= A.DL AND I_FINISH_DATE >= A.WC
                AND A.FIG_SHP = 'P158'
                AND A.FIG_NO = D.FIG_NO(+)
                AND A.SHP_COD = D.SHP_COD(+)
            ORDER BY KL;

    END TSAD001_KL_SELECT;
    
    
--------------------------------------------------------------------------------



/******************************************************************************
   - 최종수정일 : 2020-02-16
   - 최종수정자 :
   - 기능 상세 : 선표기준호선 조회 (E000)
******************************************************************************/
--------------------------------------------------------------------------------
    PROCEDURE TSAD001_KL_SELECT_E000
--------------------------------------------------------------------------------
    (
        I_FIG_NO      IN VARCHAR2,
        I_START_DATE  IN VARCHAR2,
        I_FINISH_DATE IN VARCHAR2,
        I_DOCK_FIG_NO IN VARCHAR2,
        O_CUR  OUT SYS_REFCURSOR
    )
    IS
V_USE_FLAG    VARCHAR2(10);

    BEGIN
    --중일정 중량코드 신설 관련 로직 추가(윤주원 책임,220325)--
    SELECT USE_FLAG
      INTO V_USE_FLAG
      FROM T_HCCODA
     WHERE COM_CODE = 'FIGNO';


        OPEN O_CUR FOR
         SELECT B.A_SHP_DES, B.TANDEM, B.FIG_DES, A.SHP_COD, A.FIG_NO, A.STD_SHP, A.MAK_TM
                , A.FIG_SHP AS CPY_FIG_SHP,  A.SHP_TYP_NM,  A.SHP_TYP_QTY,  A.LOA,  A.LBP,  A.WID
                , A.DEP, A.DRF_DSN, A.DRF_SCT, A.DWT_DSN, A.DWT_SCT, A.FCV_LEN, A.ACV_LEN
                , A.AFT_WID, A.TOT_TON, A.CG_TON, A.NHSW, A.SHP_VAL, A.STL_VAL, A.STL_TON
                , A.MAT_VAL, A.MHR_VAL, A.MHR_TOT, A.WST_VAL, A.ETC_VAL, A.BLD_COD
                , A.ASM_ND, A.ASM_NW, A.DCK_ND, A.DCK_NW, A.QUAY_ND, A.QUAY_NW
                , A.DCK_COD, A.WC, A.KL, A.FT, A.LC, A.DL, A.DL_YU, A.DL_GY, A.DL_NET
                , A.CNT_STS, A.SHA_PNT, A.SHA_TYP, A.SHP_GBN, A.PNT_YN, A.SHP_DES
                , A.MST_YN, A.IN_DAT, A.UP_DAT, A.IN_USR, A.UP_USR, A.SHP_KND
                , A.WCT, A.WCH, A.STD_YEA, A.BCH_NO, A.SHP_TYP, A.DL_SJ, A.DL_GR
                , A.ASM_CD, A.ASM_CW, A.ASM_CM, A.DCK_CD, A.DCK_CW, A.DCK_CM
                , A.QUAY_CD, A.QUAY_CW, A.QUAY_CM, A.BNP_GBN, A.CPY_SHP, A.OWNRP_NM
                , A.NW_GEN_FLG, A.MAK_SSTM, A.MAK_MSTM, A.MAK_LSTM, A.PMIX_YN, A.FIG_OWN_NM
                , A.PRJ_GBN, A.REF_SHP, A.SHP_TYP1, A.ADD_QUAY_TERM, A.CPY_FIG_NO, A.MHR_PRO
                , A.AF, A.FL, A.REF_FIGNO, A.KL2, A.FT2, A.FT3
                , A.KL_DCK, A.KL2_DCK, A.FT_DCK, A.FT2_DCK, A.FT3_DCK, A.NOTE, A.SC_ID
                , (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.FIG_NO AND FIG_SHP = A.FIG_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X') SUM_MUL_WGT
                , CASE WHEN A.CPY_FIG_NO != '진행' AND A.CPY_FIG_NO != '999999999' THEN
                            (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.CPY_FIG_NO AND FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X')
                       WHEN A.CPY_FIG_NO IS NOT NULL THEN
                            (SELECT CASE WHEN V_USE_FLAG = 'Y' THEN SUM(MUL_WGT2) ELSE SUM(MUL_WGT) END FROM TSEA002 WHERE FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND NVL(JOO_YN,'X') = 'X')
                       ELSE 0
                    END SUM_MUL_WGT2
                --, 0 SUM_MUL_WGT
                --, 0 SUM_MUL_WGT2
                , ''ORI_TON
                , A.EX_RATE_PRO, A.PROFIT_PRO, A.NOTE_INQ, A.NOTE_CDS
                , C.KIND, AA.BLD_COD AS DCK_BLD_COD
                , CASE WHEN A.LOA <> 0 AND AA.BLD_LEN='0' THEN 0
                       WHEN A.LOA <> 0 AND AA.BLD_LEN <> '0' THEN ROUND((AA.BLD_LEN*100)/A.LOA,1)
                       ELSE 0 END AS BLD_RATE
                , A.CPY_SHP1 AS CPY_FIG_SHP_OFT   -- 의장-모델호선
                , A.CPY_FIG_NO1 AS CPY_FIG_NO_OFT -- 의장-모델선표
                , B.A_SHP_DES_OFT                 -- 의장-모델코드
                , B.FIG_DES_OFT                   -- 의장-설명
                , CASE WHEN I_FIG_NO LIKE 'MODEL%' THEN B.A_SHP_DES ELSE A.FIG_SHP END FIG_SHP_DES, A.FIG_SHP
                , CASE WHEN A.FIG_SHP LIKE 'U%' THEN 'N'
                       WHEN C.KIND IS NULL THEN 'N'
                       WHEN C.KIND IN ('P', 'E') THEN 'Y'
                       ELSE 'N' END RUN_SHIP_INDC
        FROM TSAD001 A, TSEO151 B, (SELECT * FROM TSEG044 WHERE FIG_NO ='999999999') C
             , (--SELECT * FROM TSAF010 WHERE FIG_NO=I_DOCK_FIG_NO AND BCH_KND='2' AND FT_GBN='0'
                    SELECT CC.FIG_SHP, CASE WHEN CC.LOA <> 0 AND AA.BLD_LEN='0' THEN 0
                                            WHEN CC.LOA <> 0 AND AA.BLD_LEN <> '0' THEN ROUND((AA.BLD_LEN*100)/CC.LOA,1)
                                            ELSE 0 END AS BLD_RATE, AA.*
                    FROM TSAF010 AA, TSAD001 CC
                    WHERE AA.FIG_NO=I_DOCK_FIG_NO
                        AND AA.FIG_NO=CC.FIG_NO
                        AND AA.SHP_COD=CC.SHP_COD
                        AND AA.BCH_KND='2' AND AA.FT_GBN='0'
                        AND AA.DCK_COD=CC.DCK_COD) AA
        WHERE A.FIG_NO = B.FIG_NO(+)
            AND A.FIG_SHP = B.FIG_SHP(+)
            AND A.FIG_SHP = C.FIG_SHP(+)
--            AND A.FIG_NO  = AA.FIG_NO(+)
            AND A.SHP_COD = AA.SHP_COD(+)
--            AND A.DCK_COD = AA.DCK_COD(+)
            AND A.FIG_SHP NOT LIKE '%A'
            AND A.FIG_NO = I_FIG_NO  AND I_START_DATE <= A.DL AND I_FINISH_DATE >= A.WC
            AND A.DCK_COD IN ('6', '7')
        ORDER BY A.KL;

    END TSAD001_KL_SELECT_E000;
--------------------------------------------------------------------------------


/******************************************************************************
   - 최종수정일 : 2020-02-16
   - 최종수정자 :
   - 기능 상세 : SRC 선표 조회 - MODEL
******************************************************************************/
--------------------------------------------------------------------------------
    PROCEDURE TSAD001_FIG_NO_MODEL_SELECT
--------------------------------------------------------------------------------
    (
        I_PRJ_GBN     IN VARCHAR2,

        O_CUR  OUT SYS_REFCURSOR
    )
    IS
V_USE_FLAG    VARCHAR2(10);

    BEGIN
    --중일정 중량코드 신설 관련 로직 추가(윤주원 책임,220325)--
    SELECT USE_FLAG
      INTO V_USE_FLAG
      FROM T_HCCODA
     WHERE COM_CODE = 'FIGNO';

        OPEN O_CUR FOR
         SELECT B.A_SHP_DES, B.TANDEM, B.FIG_DES, A.SHP_COD, A.FIG_NO, A.STD_SHP, A.MAK_TM
                , A.FIG_SHP, A.SHP_TYP_NM, A.SHP_TYP_QTY, A.LOA, A.LBP, A.WID
                , A.DEP, A.DRF_DSN, A.DRF_SCT, A.DWT_DSN, A.DWT_SCT, A.FCV_LEN, A.ACV_LEN
                , A.AFT_WID, A.TOT_TON, A.CG_TON, A.NHSW, A.SHP_VAL, A.STL_VAL, A.STL_TON
                , A.MAT_VAL, A.MHR_VAL, A.MHR_TOT, A.WST_VAL, A.ETC_VAL, A.BLD_COD
                , A.ASM_ND, A.ASM_NW, A.DCK_ND, A.DCK_NW, A.QUAY_ND, A.QUAY_NW
                , A.DCK_COD, A.WC, A.KL, A.FT, A.LC, A.DL, A.DL_YU, A.DL_GY, A.DL_NET
                , A.CNT_STS, A.SHA_PNT, A.SHA_TYP, A.SHP_GBN, A.PNT_YN, A.SHP_DES
                , A.MST_YN, A.IN_DAT, A.UP_DAT, A.DE_DAT, A.IN_USR, A.UP_USR, A.SHP_KND
                , A.WCT, A.WCH, A.STD_YEA, A.BCH_NO, A.SHP_TYP, A.DL_SJ, A.DL_GR
                , A.ASM_CD, A.ASM_CW, A.ASM_CM, A.DCK_CD, A.DCK_CW, A.DCK_CM
                , A.QUAY_CD, A.QUAY_CW, A.QUAY_CM, A.BNP_GBN, A.CPY_SHP, A.OWNRP_NM
                , A.NW_GEN_FLG, A.MAK_SSTM, A.MAK_MSTM, A.MAK_LSTM, A.PMIX_YN, A.FIG_OWN_NM
                , A.PRJ_GBN, A.REF_SHP, A.SHP_TYP1, A.ADD_QUAY_TERM, A.CPY_FIG_NO, A.MHR_PRO
                , A.AF, A.FL, A.REF_FIGNO, A.KL2, A.FT2, A.FT3
                , A.KL_DCK, A.KL2_DCK, A.FT_DCK, A.FT2_DCK, A.FT3_DCK, A.NOTE, A.SC_ID
                , (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.FIG_NO AND SHP_COD = A.SHP_COD AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X') SUM_MUL_WGT
                , CASE WHEN A.CPY_FIG_NO != '진행' AND A.CPY_FIG_NO != '999999999' THEN
                            (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.CPY_FIG_NO AND FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X')
                       WHEN A.CPY_FIG_NO IS NOT NULL THEN
                            (SELECT CASE WHEN V_USE_FLAG = 'Y' THEN SUM(MUL_WGT2) ELSE SUM(MUL_WGT) END FROM TSEA002 WHERE FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND NVL(JOO_YN,'X') = 'X')
                       ELSE 0
                    END SUM_MUL_WGT2
                --, 0 SUM_MUL_WGT
                --, 0 SUM_MUL_WGT2
                , ''ORI_TON
                , A.EX_RATE_PRO, A.PROFIT_PRO, A.NOTE_INQ, A.NOTE_CDS
                , A.CPY_SHP1         -- 의장-모델호선
                , A.CPY_FIG_NO1      -- 의장-모델선표
                , B.A_SHP_DES_OFT    -- 의장-모델코드
                , B.FIG_DES_OFT      -- 의장-설명
        FROM TSAD001 A, TSEO151 B
        WHERE A.FIG_NO = B.FIG_NO(+)
            AND A.FIG_SHP = B.FIG_SHP(+)
            AND A.FIG_NO LIKE 'MODEL%'
--            AND A.DCK_COD IN ('1','2','3','4','5','8','9','G','H')
            AND (CASE WHEN NVL(I_PRJ_GBN,' ') = 'E000' AND A.DCK_COD IN ('6', '7') THEN 1
                      WHEN NVL(I_PRJ_GBN,' ') != 'E000' AND A.DCK_COD IN ('1','2','3','4','5','8','9','G','H') THEN 1 END ) = 1
        ORDER BY A_SHP_DES, KL DESC;

    END TSAD001_FIG_NO_MODEL_SELECT;
--------------------------------------------------------------------------------

--------------------------------------------------------------------------------
    PROCEDURE TSAD001_FIG_NO_MODEL_SELECT2
--------------------------------------------------------------------------------
    (
        I_PRJ_GBN     IN VARCHAR2,
        I_SHP_KND     IN VARCHAR2, -- 선종
        I_SHP_TYP_QTY IN VARCHAR2, -- 선형
        I_DCK_COD     IN VARCHAR2, -- 도크
        I_BLD_COD     IN VARCHAR2, -- 건조방식

        O_CUR  OUT SYS_REFCURSOR
    )
    IS

V_USE_FLAG    VARCHAR2(10);

    BEGIN
    --중일정 중량코드 신설 관련 로직 추가(윤주원 책임,220325)--
    SELECT USE_FLAG
      INTO V_USE_FLAG
      FROM T_HCCODA
     WHERE COM_CODE = 'FIGNO';

        OPEN O_CUR FOR
         SELECT B.A_SHP_DES, B.TANDEM, B.FIG_DES, A.SHP_COD, A.FIG_NO, A.STD_SHP, A.MAK_TM
                , A.FIG_SHP, A.SHP_TYP_NM, A.SHP_TYP_QTY, A.LOA, A.LBP, A.WID
                , A.DEP, A.DRF_DSN, A.DRF_SCT, A.DWT_DSN, A.DWT_SCT, A.FCV_LEN, A.ACV_LEN
                , A.AFT_WID, A.TOT_TON, A.CG_TON, A.NHSW, A.SHP_VAL, A.STL_VAL, A.STL_TON
                , A.MAT_VAL, A.MHR_VAL, A.MHR_TOT, A.WST_VAL, A.ETC_VAL, A.BLD_COD
                , A.ASM_ND, A.ASM_NW, A.DCK_ND, A.DCK_NW, A.QUAY_ND, A.QUAY_NW
                , A.DCK_COD, A.WC, A.KL, A.FT, A.LC, A.DL, A.DL_YU, A.DL_GY, A.DL_NET
                , A.CNT_STS, A.SHA_PNT, A.SHA_TYP, A.SHP_GBN, A.PNT_YN, A.SHP_DES
                , A.MST_YN, A.IN_DAT, A.UP_DAT, A.DE_DAT, A.IN_USR, A.UP_USR, A.SHP_KND
                , A.WCT, A.WCH, A.STD_YEA, A.BCH_NO, A.SHP_TYP, A.DL_SJ, A.DL_GR
                , A.ASM_CD, A.ASM_CW, A.ASM_CM, A.DCK_CD, A.DCK_CW, A.DCK_CM
                , A.QUAY_CD, A.QUAY_CW, A.QUAY_CM, A.BNP_GBN, A.CPY_SHP, A.OWNRP_NM
                , A.NW_GEN_FLG, A.MAK_SSTM, A.MAK_MSTM, A.MAK_LSTM, A.PMIX_YN, A.FIG_OWN_NM
                , A.PRJ_GBN, A.REF_SHP, A.SHP_TYP1, A.ADD_QUAY_TERM, A.CPY_FIG_NO, A.MHR_PRO
                , A.AF, A.FL, A.REF_FIGNO, A.KL2, A.FT2, A.FT3
                , A.KL_DCK, A.KL2_DCK, A.FT_DCK, A.FT2_DCK, A.FT3_DCK, A.NOTE, A.SC_ID
                , (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.FIG_NO AND SHP_COD = A.SHP_COD AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X') SUM_MUL_WGT
                , CASE WHEN A.CPY_FIG_NO != '진행' AND A.CPY_FIG_NO != '999999999' THEN
                            (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.CPY_FIG_NO AND FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X')
                       WHEN A.CPY_FIG_NO IS NOT NULL THEN
                            (SELECT CASE WHEN V_USE_FLAG = 'Y' THEN SUM(MUL_WGT2) ELSE SUM(MUL_WGT) END FROM TSEA002 WHERE FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND NVL(JOO_YN,'X') = 'X')
                       ELSE 0
                    END SUM_MUL_WGT2
                --, 0 SUM_MUL_WGT
                --, 0 SUM_MUL_WGT2
                , ''ORI_TON
                , A.EX_RATE_PRO, A.PROFIT_PRO, A.NOTE_INQ, A.NOTE_CDS
                , A.CPY_SHP1         -- 의장-모델호선
                , A.CPY_FIG_NO1      -- 의장-모델선표
                , B.A_SHP_DES_OFT    -- 의장-모델코드
                , B.FIG_DES_OFT      -- 의장-설명
        FROM TSAD001 A, TSEO151 B
        WHERE A.FIG_NO = B.FIG_NO(+)
            AND A.FIG_SHP = B.FIG_SHP(+)
            AND A.FIG_NO LIKE 'MODEL%'
            AND A.SHP_KND LIKE I_SHP_KND||'%' -- 선종
            --AND A.SHP_TYP_QTY LIKE I_SHP_TYP_QTY||'%' -- 선형
            AND (I_SHP_TYP_QTY IS NULL OR A.SHP_TYP_QTY = I_SHP_TYP_QTY) -- 선형
            AND A.DCK_COD LIKE I_DCK_COD||'%' -- 도크
            AND A.BLD_COD LIKE I_BLD_COD||'%' -- 건조 방식
--            AND A.DCK_COD IN ('1','2','3','4','5','8','9','G','H')
            AND (CASE WHEN NVL(I_PRJ_GBN,' ') = 'E000' AND A.DCK_COD IN ('6', '7') THEN 1
                      WHEN NVL(I_PRJ_GBN,' ') != 'E000' AND A.DCK_COD IN ('1','2','3','4','5','8','9','G','H') THEN 1 END ) = 1
        ORDER BY A_SHP_DES, KL DESC;

    END TSAD001_FIG_NO_MODEL_SELECT2;


/******************************************************************************
   - 최종수정일 : 2020-02-16
   - 최종수정자 :
   - 기능 상세 : SRC 선표 조회
******************************************************************************/
--------------------------------------------------------------------------------
    PROCEDURE TSAD001_FIG_NO_SELECT
--------------------------------------------------------------------------------
    (
        I_SRC_FIG_NO IN VARCHAR2,
        O_CUR  OUT SYS_REFCURSOR
    )
    IS
V_USE_FLAG    VARCHAR2(10);

    BEGIN
    --중일정 중량코드 신설 관련 로직 추가(윤주원 책임,220325)--
    SELECT USE_FLAG
      INTO V_USE_FLAG
      FROM T_HCCODA
     WHERE COM_CODE = 'FIGNO';

        OPEN O_CUR FOR
         SELECT B.A_SHP_DES, B.TANDEM, B.FIG_DES, A.SHP_COD, A.FIG_NO, A.STD_SHP, A.MAK_TM
                , A.FIG_SHP, A.SHP_TYP_NM, A.SHP_TYP_QTY, A.LOA, A.LBP, A.WID
                , A.DEP, A.DRF_DSN, A.DRF_SCT, A.DWT_DSN, A.DWT_SCT, A.FCV_LEN, A.ACV_LEN
                , A.AFT_WID, A.TOT_TON, A.CG_TON, A.NHSW, A.SHP_VAL, A.STL_VAL, A.STL_TON
                , A.MAT_VAL, A.MHR_VAL, A.MHR_TOT, A.WST_VAL, A.ETC_VAL, A.BLD_COD
                , A.ASM_ND, A.ASM_NW, A.DCK_ND, A.DCK_NW, A.QUAY_ND, A.QUAY_NW
                , A.DCK_COD, A.WC, A.KL, A.FT, A.LC, A.DL, A.DL_YU, A.DL_GY, A.DL_NET
                , A.CNT_STS, A.SHA_PNT, A.SHA_TYP, A.SHP_GBN, A.PNT_YN, A.SHP_DES
                , A.MST_YN, A.IN_DAT, A.UP_DAT, A.IN_USR, A.UP_USR, A.SHP_KND
                , A.WCT, A.WCH, A.STD_YEA, A.BCH_NO, A.SHP_TYP, A.DL_SJ, A.DL_GR
                , A.ASM_CD, A.ASM_CW, A.ASM_CM, A.DCK_CD, A.DCK_CW, A.DCK_CM
                , A.QUAY_CD, A.QUAY_CW, A.QUAY_CM, A.BNP_GBN, A.CPY_SHP, A.OWNRP_NM
                , A.NW_GEN_FLG, A.MAK_SSTM, A.MAK_MSTM, A.MAK_LSTM, A.PMIX_YN, A.FIG_OWN_NM
                , A.PRJ_GBN, A.REF_SHP, A.SHP_TYP1, A.ADD_QUAY_TERM, A.CPY_FIG_NO, A.MHR_PRO
                , A.AF, A.FL, A.REF_FIGNO, A.KL2, A.FT2, A.FT3
                , A.KL_DCK, A.KL2_DCK, A.FT_DCK, A.FT2_DCK, A.FT3_DCK, A.NOTE, A.SC_ID
                , (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.FIG_NO AND SHP_COD = A.SHP_COD AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X') SUM_MUL_WGT
                , CASE WHEN A.CPY_FIG_NO != '진행' AND A.CPY_FIG_NO != '999999999' THEN
                            (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.CPY_FIG_NO AND FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X')
                       WHEN A.CPY_FIG_NO IS NOT NULL THEN
                            (SELECT CASE WHEN V_USE_FLAG = 'Y' THEN SUM(MUL_WGT2) ELSE SUM(MUL_WGT) END FROM TSEA002 WHERE FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND NVL(JOO_YN,'X') = 'X')
                       ELSE 0
                    END SUM_MUL_WGT2
                --, 0 SUM_MUL_WGT
                --, 0 SUM_MUL_WGT2
                , ''ORI_TON
                , A.CPY_SHP1         -- 의장-모델호선
                , A.CPY_FIG_NO1      -- 의장-모델선표
                , B.A_SHP_DES_OFT    -- 의장-모델코드
                , B.FIG_DES_OFT      -- 의장-설명
        FROM TSAD001 A, TSEO151 B
        WHERE A.FIG_NO = B.FIG_NO(+)
            AND A.FIG_SHP = B.FIG_SHP(+)
            AND A.FIG_SHP NOT LIKE '%A'
            AND A.FIG_NO = I_SRC_FIG_NO
        ORDER BY A.FIG_NO, A.FIG_SHP;

    END TSAD001_FIG_NO_SELECT;

--------------------------------------------------------------------------------
    PROCEDURE TSAD001_FIG_NO_SELECT2
--------------------------------------------------------------------------------
    (
        I_SRC_FIG_NO IN VARCHAR2,
        I_SHP_KND     IN VARCHAR2, -- 선종
        I_SHP_TYP_QTY IN VARCHAR2, -- 선형
        I_DCK_COD     IN VARCHAR2, -- 도크
        I_BLD_COD     IN VARCHAR2, -- 건조방식

        O_CUR  OUT SYS_REFCURSOR
    )
    IS
V_USE_FLAG    VARCHAR2(10);

    BEGIN
    --중일정 중량코드 신설 관련 로직 추가(윤주원 책임,220325)--
    SELECT USE_FLAG
      INTO V_USE_FLAG
      FROM T_HCCODA
     WHERE COM_CODE = 'FIGNO';

        OPEN O_CUR FOR
         SELECT B.A_SHP_DES, B.TANDEM, B.FIG_DES, A.SHP_COD, A.FIG_NO, A.STD_SHP, A.MAK_TM
                , A.FIG_SHP, A.SHP_TYP_NM, A.SHP_TYP_QTY, A.LOA, A.LBP, A.WID
                , A.DEP, A.DRF_DSN, A.DRF_SCT, A.DWT_DSN, A.DWT_SCT, A.FCV_LEN, A.ACV_LEN
                , A.AFT_WID, A.TOT_TON, A.CG_TON, A.NHSW, A.SHP_VAL, A.STL_VAL, A.STL_TON
                , A.MAT_VAL, A.MHR_VAL, A.MHR_TOT, A.WST_VAL, A.ETC_VAL, A.BLD_COD
                , A.ASM_ND, A.ASM_NW, A.DCK_ND, A.DCK_NW, A.QUAY_ND, A.QUAY_NW
                , A.DCK_COD, A.WC, A.KL, A.FT, A.LC, A.DL, A.DL_YU, A.DL_GY, A.DL_NET
                , A.CNT_STS, A.SHA_PNT, A.SHA_TYP, A.SHP_GBN, A.PNT_YN, A.SHP_DES
                , A.MST_YN, A.IN_DAT, A.UP_DAT, A.IN_USR, A.UP_USR, A.SHP_KND
                , A.WCT, A.WCH, A.STD_YEA, A.BCH_NO, A.SHP_TYP, A.DL_SJ, A.DL_GR
                , A.ASM_CD, A.ASM_CW, A.ASM_CM, A.DCK_CD, A.DCK_CW, A.DCK_CM
                , A.QUAY_CD, A.QUAY_CW, A.QUAY_CM, A.BNP_GBN, A.CPY_SHP, A.OWNRP_NM
                , A.NW_GEN_FLG, A.MAK_SSTM, A.MAK_MSTM, A.MAK_LSTM, A.PMIX_YN, A.FIG_OWN_NM
                , A.PRJ_GBN, A.REF_SHP, A.SHP_TYP1, A.ADD_QUAY_TERM, A.CPY_FIG_NO, A.MHR_PRO
                , A.AF, A.FL, A.REF_FIGNO, A.KL2, A.FT2, A.FT3
                , A.KL_DCK, A.KL2_DCK, A.FT_DCK, A.FT2_DCK, A.FT3_DCK, A.NOTE, A.SC_ID
                , (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.FIG_NO AND SHP_COD = A.SHP_COD AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X') SUM_MUL_WGT
                , CASE WHEN A.CPY_FIG_NO != '진행' AND A.CPY_FIG_NO != '999999999' THEN
                            (SELECT SUM(MUL_WGT) FROM TSEG005 WHERE FIG_NO = A.CPY_FIG_NO AND FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND JOO_YN = 'X')
                       WHEN A.CPY_FIG_NO IS NOT NULL THEN
                            (SELECT CASE WHEN V_USE_FLAG = 'Y' THEN SUM(MUL_WGT2) ELSE SUM(MUL_WGT) END FROM TSEA002 WHERE FIG_SHP = A.CPY_SHP AND WRK_STG = 'A' AND WRK_TYP = '02' AND NVL(JOO_YN,'X') = 'X')
                       ELSE 0
                    END SUM_MUL_WGT2
                --, 0 SUM_MUL_WGT
                --, 0 SUM_MUL_WGT2
                , ''ORI_TON
                , A.CPY_SHP1         -- 의장-모델호선
                , A.CPY_FIG_NO1      -- 의장-모델선표
                , B.A_SHP_DES_OFT    -- 의장-모델코드
                , B.FIG_DES_OFT      -- 의장-설명
        FROM TSAD001 A, TSEO151 B
        WHERE A.FIG_NO = B.FIG_NO(+)
            AND A.FIG_SHP = B.FIG_SHP(+)
            AND A.FIG_SHP NOT LIKE '%A'
            AND A.FIG_NO = I_SRC_FIG_NO
            AND A.SHP_KND LIKE I_SHP_KND||'%' -- 선종
            AND A.SHP_TYP_QTY LIKE I_SHP_TYP_QTY||'%' -- 선형
            AND A.DCK_COD LIKE I_DCK_COD||'%' -- 도크
            AND A.BLD_COD LIKE I_BLD_COD||'%' -- 건조 방식
        ORDER BY A.FIG_NO, A.FIG_SHP;

    END TSAD001_FIG_NO_SELECT2;

/******************************************************************************
   - 기능 상세 : 사업계획 데이터 복사/삭제 대상 목록 생성
******************************************************************************/
    PROCEDURE COPY_DEL_CREATE_LIST
    (
        I_FIG_NO            IN VARCHAR2,
        I_SHP_COD           IN VARCHAR2,
        I_FIG_SHP           IN VARCHAR2,
        I_CPY_FIG_NO        IN VARCHAR2,
        I_CPY_FIG_NO_OFT    IN VARCHAR2,
        I_CPY_SHP           IN VARCHAR2,
        I_CPY_FIG_SHP_OFT   IN VARCHAR2,
        I_DCK_COD           IN VARCHAR2,
        I_RUNDATA           IN VARCHAR2,
        I_TANDEM            IN VARCHAR2,
        I_FIG_DES           IN VARCHAR2,
        I_FIG_DES_OFT       IN VARCHAR2,
        I_A_SHP_DES         IN VARCHAR2,
        I_A_SHP_DES_OFT     IN VARCHAR2,

        I_WORKING_STATE     IN VARCHAR2,
        I_SUM_MUL_WGT       IN VARCHAR2,
        I_SUM_MUL_WGT2      IN VARCHAR2, -- 보정 중량
        I_ORI_TON           IN VARCHAR2,
        I_ERECSHIFT         IN VARCHAR2,
        I_ACT_PLN_YN        IN VARCHAR2, -- Act.일정변경 체크 박스
        I_USER_ID           IN VARCHAR2,

        O_APP_CODE          OUT VARCHAR2,
        O_APP_MSG           OUT VARCHAR2
    )
    IS

    BEGIN

        -- 대상목록 삭제
        DELETE
          FROM TSMG035
         WHERE FIG_NO = I_FIG_NO
           AND SHP_COD = I_SHP_COD;


        -- 대상목록 생성
        INSERT INTO TSMG035(FIG_NO
                          , SHP_COD
                          , FIG_SHP
                          , CPY_FIG_NO
                          , CPY_FIG_NO_OFT
                          , CPY_FIG_SHP
                          , CPY_FIG_SHP_OFT
                          , DCK_COD
                          , RUNDATA
                          , TANDEM
                          , FIG_DES
                          , FIG_DES_OFT
                          , A_SHP_DES
                          , A_SHP_DES_OFT
                          , WORKING_STAGE
                          , SUM_MUL_WGT
                          , SUM_MUL_WGT2
                          , ORI_TON
                          , ERECSHIFT
                          , ACT_PLN_YN
                          , USER_ID)
                     VALUES (I_FIG_NO
                          , I_SHP_COD
                          , I_FIG_SHP
                          , I_CPY_FIG_NO
                          , I_CPY_FIG_NO_OFT
                          , I_CPY_SHP
                          , I_CPY_FIG_SHP_OFT
                          , I_DCK_COD
                          , I_RUNDATA
                          , I_TANDEM
                          , I_FIG_DES
                          , I_FIG_DES_OFT
                          , I_A_SHP_DES
                          , I_A_SHP_DES_OFT
                          , I_WORKING_STATE
                          , I_SUM_MUL_WGT
                          , I_SUM_MUL_WGT2
                          , I_ORI_TON
                          , I_ERECSHIFT
                          , I_ACT_PLN_YN
                          , I_USER_ID);

        O_APP_CODE := '0';
        O_APP_MSG  := 'OK';

     EXCEPTION WHEN OTHERS THEN
        O_APP_CODE := '-1';
        O_APP_MSG  := SQLERRM;

    END COPY_DEL_CREATE_LIST;

/******************************************************************************
   - 기능 상세 : 사업계획 데이터 복사/삭제 대상 목록 멀티 프로세스 지정
******************************************************************************/
    PROCEDURE COPY_DEL_UPDATE_LIST
    (
        I_FIG_NO            IN VARCHAR2,

        O_APP_CODE          OUT VARCHAR2,
        O_APP_MSG           OUT VARCHAR2
    )
    IS

    BEGIN

      UPDATE TSMG035
         SET GUBN = CASE WHEN MOD(ROWNUM, 10) = 0 THEN 10 ELSE MOD(ROWNUM, 10) END
       WHERE FIG_NO = I_FIG_NO
         AND SDATE IS NULL
         AND EDATE IS NULL;

        O_APP_CODE := '0';
        O_APP_MSG  := 'OK';

     EXCEPTION WHEN OTHERS THEN
        O_APP_CODE := '-1';
        O_APP_MSG  := SQLERRM;

    END COPY_DEL_UPDATE_LIST;

/******************************************************************************
   - 기능 상세 : 사업계획 데이터 복사/삭제
******************************************************************************/
--------------------------------------------------------------------------------
    PROCEDURE COPY_DEL_SHIPTICKETDATA
--------------------------------------------------------------------------------
    (
        I_FIG_NO        IN VARCHAR2,
        I_GUBN          IN VARCHAR2,
        I_USRID         IN VARCHAR2,

        O_APP_CODE      OUT VARCHAR2,
        O_APP_MSG       OUT VARCHAR2,
        O_RESULT        OUT VARCHAR2,
        O_HASCOPYSHIP   OUT VARCHAR2,
        O_SUM_MGL_WGT   OUT VARCHAR2
    )
    IS
        V_PROC_GB VARCHAR2(1);
        V_CNT NUMBER(5) := 0;

    BEGIN

    -- OUT변수 초기화
    O_APP_CODE := '0';
    O_APP_MSG  := 'OK';
    O_RESULT := 'F';
    O_HASCOPYSHIP := 'N';
    O_SUM_MGL_WGT := 0;

    FOR V IN
    (
      SELECT FIG_NO
           , SHP_COD
           , FIG_SHP
           , CPY_FIG_NO
           , CPY_FIG_NO_OFT
           , CPY_FIG_SHP
           , CPY_FIG_SHP_OFT
           , DCK_COD
           , RUNDATA
           , TANDEM
           , FIG_DES
           , FIG_DES_OFT
           , A_SHP_DES
           , A_SHP_DES_OFT
           , WORKING_STAGE
           , SUM_MUL_WGT  -- 현재 중량
           , SUM_MUL_WGT2 -- 보정 중량
           , ORI_TON
           , ERECSHIFT
           , ACT_PLN_YN
           , USER_ID
        FROM TSMG035
       WHERE FIG_NO  = I_FIG_NO
         AND GUBN    = I_GUBN
         AND USER_ID = I_USRID
         AND SDATE IS NULL
         AND EDATE IS NULL
    )
    LOOP
        V_CNT := V_CNT + 1;

        -- START 시간 저장
        UPDATE TSMG035
           SET SDATE = TO_CHAR(SYSDATE, 'YYYYMMDDHH24MISS')
         WHERE FIG_NO = V.FIG_NO
           AND SHP_COD = V.SHP_COD;

        -- 삭제인 경우
        IF V.WORKING_STAGE = 'D' THEN

            PROC_DELETE_PROJECT_DATA2(V.FIG_NO, V.SHP_COD, V.FIG_SHP);

            DELETE TSEO151
             WHERE FIG_NO = V.FIG_NO
               AND FIG_SHP = V.FIG_SHP;

            O_RESULT := 'N';
            O_HASCOPYSHIP := 'N';

         -- 사업계획 데이터 복사인 경우
         ELSE

            V_PROC_GB := CASE WHEN V.CPY_FIG_NO  = '999999999' AND V.CPY_FIG_NO_OFT  = '999999999' THEN 'A'   -- 선각/의장 모두 진행
                              WHEN V.CPY_FIG_NO  = '999999999' AND V.CPY_FIG_NO_OFT != '999999999' THEN 'H'   -- 선각만 진행
                              WHEN V.CPY_FIG_NO != '999999999' AND V.CPY_FIG_NO_OFT  = '999999999' THEN 'O'   -- 의장만 진행
                              ELSE 'N'   -- 선각 의장 모두 진행 아님
                         END;


            -- 선각/의장 모두 진행 호선에서 복사
            IF V_PROC_GB = 'A' THEN

                -- 선각
                COPY_SHIPTICKETDATA_RUN(V.FIG_NO,  V.SHP_COD,    V.FIG_SHP, V.CPY_FIG_NO, V.CPY_FIG_SHP, V.DCK_COD,
                                        V.RUNDATA, V.ACT_PLN_YN, V.USER_ID, 'H',          O_APP_CODE,    O_APP_MSG, O_RESULT, O_HASCOPYSHIP);

                IF O_APP_CODE <> '0' THEN
                    RETURN;
                END IF;

                -- 의장
                COPY_SHIPTICKETDATA_RUN(V.FIG_NO,  V.SHP_COD,    V.FIG_SHP, V.CPY_FIG_NO_OFT, V.CPY_FIG_SHP_OFT, V.DCK_COD,
                                        V.RUNDATA, V.ACT_PLN_YN, V.USER_ID, 'O',          O_APP_CODE, O_APP_MSG, O_RESULT, O_HASCOPYSHIP);
            -- 선각만 진행 호선에서 복사
            ELSIF V_PROC_GB = 'H' THEN
                -- 선각
                COPY_SHIPTICKETDATA_RUN(V.FIG_NO, V.SHP_COD, V.FIG_SHP, V.CPY_FIG_NO, V.CPY_FIG_SHP, V.DCK_COD,
                                        V.RUNDATA, V.ACT_PLN_YN, V.USER_ID, 'H', O_APP_CODE, O_APP_MSG, O_RESULT, O_HASCOPYSHIP);

                IF O_APP_CODE <> '0' THEN
                    RETURN;
                END IF;

                -- 의장
                COPY_SHIPTICKETDATA(V.FIG_NO, V.SHP_COD, V.FIG_SHP, V.CPY_FIG_NO_OFT, V.CPY_FIG_SHP_OFT, V.DCK_COD,
                                    V.TANDEM, V.FIG_DES, V.A_SHP_DES, V.ERECSHIFT, V.ACT_PLN_YN, V.USER_ID, 'O', O_APP_CODE, O_APP_MSG, O_RESULT, O_HASCOPYSHIP);
            -- 의장만 진행 호선에서 복사
            ELSIF V_PROC_GB = 'O' THEN
                -- 선각
                COPY_SHIPTICKETDATA(V.FIG_NO, V.SHP_COD, V.FIG_SHP, V.CPY_FIG_NO, V.CPY_FIG_SHP, V.DCK_COD,
                                    V.TANDEM, V.FIG_DES, V.A_SHP_DES, V.ERECSHIFT, V.ACT_PLN_YN, V.USER_ID, 'H', O_APP_CODE, O_APP_MSG, O_RESULT, O_HASCOPYSHIP);

                IF O_APP_CODE <> '0' THEN
                    RETURN;
                END IF;

                -- 의장
                COPY_SHIPTICKETDATA_RUN(V.FIG_NO, V.SHP_COD, V.FIG_SHP, V.CPY_FIG_NO_OFT, V.CPY_FIG_SHP_OFT, V.DCK_COD,
                                        V.RUNDATA, V.ACT_PLN_YN, V.USER_ID, 'O', O_APP_CODE, O_APP_MSG, O_RESULT, O_HASCOPYSHIP);

            -- 선각/의장 모두 모델선에서 복사
            ELSE
                -- 선각
                COPY_SHIPTICKETDATA(V.FIG_NO, V.SHP_COD, V.FIG_SHP, V.CPY_FIG_NO, V.CPY_FIG_SHP, V.DCK_COD,
                                    V.TANDEM, V.FIG_DES, V.A_SHP_DES, V.ERECSHIFT, V.ACT_PLN_YN, V.USER_ID, 'H', O_APP_CODE, O_APP_MSG, O_RESULT, O_HASCOPYSHIP);

                IF O_APP_CODE <> '0' THEN
                    RETURN;
                END IF;

                -- 의장
                COPY_SHIPTICKETDATA(V.FIG_NO, V.SHP_COD, V.FIG_SHP, V.CPY_FIG_NO_OFT, V.CPY_FIG_SHP_OFT, V.DCK_COD,
                                    V.TANDEM, V.FIG_DES, V.A_SHP_DES, V.ERECSHIFT, V.ACT_PLN_YN, V.USER_ID, 'O', O_APP_CODE, O_APP_MSG, O_RESULT, O_HASCOPYSHIP);

            END IF;

            IF O_APP_CODE <> '0' THEN
                RETURN;
            END IF;

            -- 보정 중량을 입력한 경우 중량 보정
            IF NVL(TRIM(V.SUM_MUL_WGT2),0) > 0 THEN
                OPT.PROC_MOD_MULWGT_BP(V.FIG_NO, V.FIG_SHP, V.SHP_COD, TO_NUMBER(V.SUM_MUL_WGT2), O_APP_MSG);
                O_SUM_MGL_WGT := V.SUM_MUL_WGT2;
            END IF;

            -- 초기 중량 저장
            IF NVL(TRIM(V.SUM_MUL_WGT2),0) > 0 AND NVL(TRIM(V.SUM_MUL_WGT2),0) <> NVL(TRIM(V.SUM_MUL_WGT),0) THEN
                UPDATE TSAD001
                   SET ORI_TON = V.SUM_MUL_WGT
                 WHERE SHP_COD = V.SHP_COD
                   AND FIG_NO = V.FIG_NO;
            END IF;

        END IF;

        -- END 시간 저장
        UPDATE TSMG035
           SET EDATE = TO_CHAR(SYSDATE, 'YYYYMMDDHH24MISS')
         WHERE FIG_NO = V.FIG_NO
           AND SHP_COD = V.SHP_COD;

    END LOOP;

    O_APP_CODE := '0';
    O_APP_MSG  := 'OK:'||V_CNT;

    EXCEPTION WHEN OTHERS THEN
        O_APP_CODE := '-1';
        O_APP_MSG  := O_APP_MSG || ' : ' ||SQLERRM;
        O_RESULT := 'F';
        O_HASCOPYSHIP := 'N';
        O_SUM_MGL_WGT := 0;
    END COPY_DEL_SHIPTICKETDATA;
--------------------------------------------------------------------------------


/******************************************************************************
   - 최종수정일 : 2020-02-19
   - 최종수정자 :
   - 기능 상세 : 진행 호선에서 사업계획 데이터 복사
******************************************************************************/
    PROCEDURE COPY_SHIPTICKETDATA_RUN
    (
        I_FIG_NO        IN VARCHAR2,    -- To 선표
        I_SHP_COD       IN VARCHAR2,    -- 호선코드 (내부)
        I_FIG_SHP       IN VARCHAR2,    -- To 호선
        I_CPY_FIG_NO    IN VARCHAR2,    -- From 선표 / '999999999' = 진행
--        I_CPY_SHP_COD   IN VARCHAR2,
        I_CPY_SHP       IN VARCHAR2,    -- From 호선
        I_DCK_COD       IN VARCHAR2,    -- 도크
        I_RUNDATA       IN VARCHAR2,    -- 진행호선 여부 (Y/N)
        I_ACT_PLN_YN    IN VARCHAR2,    -- Act.일정변경 (Y/N)
        I_USER_ID       IN VARCHAR2,    -- 입력자
        I_GB            IN VARCHAR2,    -- 선각(H)/의장(O) 구분
        O_APP_CODE      OUT VARCHAR2,   -- 오류 코드
        O_APP_MSG       OUT VARCHAR2,   -- 오류 메세지
        O_RESULT        OUT VARCHAR2,   -- 결과 메세지 (N/F)
        O_HASCOPYSHIP   OUT VARCHAR2    -- 선표 복사 성공 여부 (Y/N)
    )
    IS
        V_PROC VARCHAR2(100) := 'COPY_SHIPTICKETDATA_RUN';
        V_CPY_FIG_NO   TSAD001.CPY_FIG_NO%TYPE;
        V_CPY_SHP      TSAD001.CPY_SHP%TYPE;
        V_CPY_SHP_COD  TSAA002.SHP_COD%TYPE;
        V_CNT NUMBER := 0;
        V_CNT1 NUMBER := 0;
        V_CNT2 NUMBER := 0;
        V_GUBN VARCHAR2(10);

    BEGIN

        SELECT MAX(SHP_COD)
          INTO V_CPY_SHP_COD
          FROM TSAA002
         WHERE FIG_SHP = I_CPY_SHP;

        IF V_CPY_SHP_COD IS NOT NULL THEN

            V_CPY_SHP := I_CPY_SHP;
            V_CPY_FIG_NO := '999999999';

            SELECT SHP_COD
              INTO V_CPY_SHP_COD
              FROM TSAA002
             WHERE FIG_SHP = I_CPY_SHP;

        ELSE
            O_APP_CODE := '-1';
            O_APP_MSG  := V_PROC || ', 0 : 진행 호선 없음('||I_CPY_SHP||', 구분:'||I_GB||' )';
            O_RESULT := 'F';
            O_HASCOPYSHIP := 'N';
            RETURN;
        END IF;

        V_GUBN := CASE WHEN I_GB = 'H' THEN '선각' ELSE '의장' END;

        -- 0 -----------------------------

        O_APP_CODE := '1-1';
        O_APP_MSG  := '진행 호선 ' || I_GB || ' 중일정 여부 체크';

        -- 1-1 ---------------------------

        -- 선각
        IF I_GB = 'H' THEN
            SELECT COUNT(*)
              INTO V_CNT
              FROM TSEA002
             WHERE SHP_COD = V_CPY_SHP_COD;

        -- 의장
        ELSE
            SELECT COUNT(*)
              INTO V_CNT1
              FROM TSFA001
             WHERE SHP_COD = V_CPY_SHP_COD
               AND WRK_STG NOT IN ('P', 'Q', 'R');

            SELECT COUNT(*)
              INTO V_CNT2
              FROM TSFN101
             WHERE CASE_NO = '000000000000'
               AND SHP_COD = V_CPY_SHP_COD;

            V_CNT := V_CNT1 + V_CNT2;
        END IF;

        -- 진행호선의 중일정이 있는 경우만 진행호선에서 데이터를 복사해온다.
        IF V_CNT > 0 THEN

            O_APP_CODE := '1-2';
            O_APP_MSG  := '사업계획 데이터 복사';

            BEGIN

                IF I_GB = 'H' THEN
                    PROC_COPY_PROJECT_DATA1_H (I_FIG_NO, I_SHP_COD, I_FIG_SHP, V_CPY_FIG_NO, V_CPY_SHP_COD, V_CPY_SHP, I_DCK_COD, I_USER_ID, I_ACT_PLN_YN, O_APP_MSG);
                ELSE
                    PROC_COPY_PROJECT_DATA1_O (I_FIG_NO, I_SHP_COD, I_FIG_SHP, V_CPY_FIG_NO, V_CPY_SHP_COD, V_CPY_SHP, I_DCK_COD, I_USER_ID, I_ACT_PLN_YN, O_APP_MSG);
                END IF;


                IF O_APP_MSG <> 'OK' THEN
                    O_APP_CODE := '-1';
                    RETURN;
                END IF;

              O_RESULT := 'N';
              O_HASCOPYSHIP := 'Y';

            EXCEPTION
                WHEN NO_DATA_FOUND THEN
                    O_APP_CODE := '0';
                    O_APP_MSG  := 'OK';
                WHEN OTHERS THEN
                  PROC_DELETE_PROJECT_DATA2(I_FIG_NO, I_SHP_COD, I_FIG_SHP);

                  O_RESULT := 'F';
                  O_HASCOPYSHIP := 'N';
                  O_APP_CODE := '-1';
                  O_APP_MSG  := V_PROC || ', 1-2 : 사업계획 데이터 복사 실패:I_GB='||I_GB||', V_CPY_SHP_COD='||V_CPY_SHP_COD||', '||SQLERRM;
                  RETURN;
            END;

            O_APP_CODE := '0';
            O_APP_MSG  := 'OK';

        -- 진행호선 중일정이 없으면 오류
        ELSE
            O_APP_CODE := '-1';
            O_APP_MSG  := V_PROC || ', 1-1 : 진행 호선 :'|| V_CPY_SHP_COD || ' - ' || V_GUBN || ' 중일정 없음';
            O_RESULT   := 'F';
            O_HASCOPYSHIP := 'N';
        END IF;

    EXCEPTION WHEN OTHERS THEN
        O_APP_CODE := '-1';
        O_APP_MSG  := V_PROC || SQLERRM;
        O_RESULT := 'F';
        O_HASCOPYSHIP := 'N';

    END COPY_SHIPTICKETDATA_RUN;
--------------------------------------------------------------------------------

--------------------------------------------------------------------------------
/******************************************************************************
   - 최종수정일 : 2020-02-19
   - 최종수정자 :
   - 기능 상세 : 모델선에서 사업계획 데이터 복사
******************************************************************************/
    PROCEDURE COPY_SHIPTICKETDATA
--------------------------------------------------------------------------------
    (
        I_FIG_NO           IN VARCHAR2,
        I_SHP_COD          IN VARCHAR2,
        I_FIG_SHP          IN VARCHAR2,
        I_CPY_FIG_NO       IN VARCHAR2,
        I_CPY_SHP          IN VARCHAR2,
        I_DCK_COD          IN VARCHAR2,
        I_TANDEM           IN VARCHAR2,
        I_FIG_DES          IN VARCHAR2,
        I_A_SHP_DES        IN VARCHAR2,
        I_ERECSHIFT        IN VARCHAR2,
        I_ACT_PLN_YN       IN VARCHAR2,
        I_USER_ID          IN VARCHAR2,
        I_GB               IN VARCHAR2,    -- 선각(H)/의장(O) 구분
        O_APP_CODE         OUT VARCHAR2,
        O_APP_MSG          OUT VARCHAR2,
        O_RESULT           OUT VARCHAR2,
        O_HASCOPYSHIP      OUT VARCHAR2
    )
    IS
        V_PROC VARCHAR2(100) := 'COPY_SHIPTICKETDATA';
        V_CPY_SHP_COD TSAA002.SHP_COD%TYPE;
        V_CNT NUMBER := 0;
        V_CNT1 NUMBER := 0;
        V_CNT2 NUMBER := 0;
        V_CNT3 NUMBER := 0;
        V_GAP NUMBER := 0;

        V_APP_MSG VARCHAR2(1000);
    BEGIN

        O_APP_CODE := '0';
        V_APP_MSG  := '선표 여부 체크';

        SELECT COUNT(A.FIG_NO)
          INTO V_CNT
          FROM TSAD001 A, TSEO151 B
         WHERE A.FIG_NO  = B.FIG_NO(+)
           AND A.FIG_SHP = B.FIG_SHP(+)
           AND A.FIG_SHP NOT LIKE '%A'
           AND trim(A.FIG_NO)  = trim(I_CPY_FIG_NO) --2023-10-23 양쪽 트림
           AND A.FIG_SHP = I_CPY_SHP;

        IF V_CNT > 0 THEN
            SELECT A.SHP_COD
              INTO V_CPY_SHP_COD
              FROM TSAD001 A, TSEO151 B
             WHERE A.FIG_NO  = B.FIG_NO(+)
               AND A.FIG_SHP = B.FIG_SHP(+)
               AND A.FIG_SHP NOT LIKE '%A'
               AND trim(A.FIG_NO)  = trim(I_CPY_FIG_NO) --2023-10-23 양쪽 트림
               AND A.FIG_SHP = I_CPY_SHP;
        ELSE
            O_APP_CODE := '-1';
            O_APP_MSG  := V_PROC || ', 0 : 선표 데이터 없음, I_CPY_SHP='||I_CPY_SHP||', I_CPY_FIG_NO='||I_CPY_FIG_NO;
            O_RESULT := 'F';
            O_HASCOPYSHIP := 'N';
            RETURN;
        END IF;


        -- 0 -----------------------------

        O_APP_CODE := '1-1';
        V_APP_MSG  := '사업계획 호선 중일정 여부 체크';
        -- 1-1 ---------------------------

        -- 선각
        IF I_GB = 'H' THEN
            SELECT COUNT(FIG_NO)
              INTO V_CNT
              FROM TSEG005

             WHERE FIG_NO = I_CPY_FIG_NO
               AND SHP_COD = V_CPY_SHP_COD;

        -- 의장
        ELSE
            SELECT COUNT(*)
              INTO V_CNT1
              FROM TSMG_TSFA001
             WHERE SHP_COD = V_CPY_SHP_COD
               AND WRK_STG NOT IN ('P', 'Q', 'R');

            SELECT COUNT(*)
              INTO V_CNT2
              FROM TSMG_TSFN101
             WHERE CASE_NO = '000000000000'
               AND SHP_COD = V_CPY_SHP_COD;

            V_CNT := V_CNT1 + V_CNT2;
        END IF;

        IF V_CNT > 0 THEN
            O_APP_CODE := '1-2';
            V_APP_MSG  := '사업계획 데이터 복사';

            BEGIN
                V_APP_MSG  := '사업계획 데이터 복사1.1('||I_GB||')';
                IF I_GB = 'H' THEN
                    PROC_COPY_PROJECT_DATA2_H (I_FIG_NO, I_SHP_COD, I_FIG_SHP, I_CPY_FIG_NO, V_CPY_SHP_COD, I_CPY_SHP, I_DCK_COD, I_USER_ID, I_ACT_PLN_YN, O_APP_MSG);
                ELSE
                    PROC_COPY_PROJECT_DATA2_O (I_FIG_NO, I_SHP_COD, I_FIG_SHP, I_CPY_FIG_NO, V_CPY_SHP_COD, I_CPY_SHP, I_DCK_COD, I_USER_ID, I_ACT_PLN_YN, O_APP_MSG);
                END IF;
                
                IF O_APP_MSG <> 'OK' THEN
                    RETURN;
                END IF;

                IF I_A_SHP_DES <> '' THEN
                    V_APP_MSG  := '사업계획 데이터 복사1.2('||I_GB||')';
                    SELECT COUNT(*)
                      INTO V_CNT3
                      FROM TSEO151
                     WHERE FIG_NO = I_FIG_NO
                       AND FIG_SHP = I_FIG_SHP;

                    IF V_CNT3 > 0 THEN
                        V_APP_MSG  := '사업계획 데이터 복사1.3('||I_GB||')';
                        IF I_GB = 'H' THEN
                            UPDATE TSEO151
                               SET TANDEM = I_TANDEM
                                 , FIG_DES = I_FIG_DES
                                 , IN_DAT = TO_CHAR(SYSDATE, 'YYYYMMDDHH24MI')
                                 , IN_USR = I_USER_ID
                                 , A_SHP_DES = I_A_SHP_DES
                             WHERE FIG_NO = I_FIG_NO
                               AND FIG_SHP = I_FIG_SHP;
                        ELSE
                            UPDATE TSEO151
                               SET TANDEM = I_TANDEM
                                 , FIG_DES_OFT = I_FIG_DES
                                 , IN_DAT = TO_CHAR(SYSDATE, 'YYYYMMDDHH24MI')
                                 , IN_USR = I_USER_ID
                                 , A_SHP_DES_OFT = I_A_SHP_DES
                             WHERE FIG_NO = I_FIG_NO
                               AND FIG_SHP = I_FIG_SHP;
                        END IF;

                    ELSE
                        V_APP_MSG  := '사업계획 데이터 복사1.4('||I_GB||')';
                        IF I_GB = 'H' THEN
                            INSERT INTO TSEO151 (FIG_NO, TANDEM, FIG_SHP, FIG_DES, IN_DAT, IN_USR, A_SHP_DES)
                            VALUES (I_FIG_NO, I_TANDEM, I_FIG_SHP, I_FIG_DES, TO_CHAR(SYSDATE, 'YYYYMMDDHH24MI'), I_USER_ID, I_A_SHP_DES);
                        ELSE
                            INSERT INTO TSEO151 (FIG_NO, TANDEM, FIG_SHP, FIG_DES_OFT, IN_DAT, IN_USR, A_SHP_DES_OFT)
                            VALUES (I_FIG_NO, I_TANDEM, I_FIG_SHP, I_FIG_DES, TO_CHAR(SYSDATE, 'YYYYMMDDHH24MI'), I_USER_ID, I_A_SHP_DES);
                        END IF;

                END IF;
            END IF;

            O_RESULT := 'N';
            O_HASCOPYSHIP := 'Y';

            EXCEPTION WHEN OTHERS THEN --result -1
                PROC_DELETE_PROJECT_DATA2(I_FIG_NO, I_SHP_COD, I_FIG_SHP);

                O_RESULT := 'F';
                O_HASCOPYSHIP := 'N';
                O_APP_CODE := '-1';
                O_APP_MSG  := '복사 실패 : ' || V_APP_MSG ||','|| SQLERRM;
                RETURN;
            END;
        -- 1-2 ---------------------------
        ELSE
            O_APP_CODE := '-1';
            O_APP_MSG  := V_PROC || ', 1-1 : 사업계획 호선 중일정 없음';
            O_RESULT := 'F';
            O_HASCOPYSHIP := 'N';
            -- 2022.11.08 선표 복사시 FROM 선표에 중일정이 없어도 넘어가도록 주석처리
            --RETURN;
        END IF;

        -- 1-1 ---------------------------

--         IF I_A_SHP_DES = '' THEN
--            DELETE FROM TSEO151
--             WHERE FIG_NO = I_FIG_NO
--               AND FIG_SHP = I_FIG_SHP;
--         END IF;

         --UpdateErectionGaop
        IF I_ERECSHIFT = 'Y' THEN
            BEGIN
                PKG_PP009_LTS004.UPDATE_ERECTIONGAOP(I_FIG_NO, I_FIG_SHP, I_CPY_FIG_NO, I_CPY_SHP, I_SHP_COD);
            EXCEPTION WHEN OTHERS THEN
                O_APP_CODE := '-1';
                O_APP_MSG  := V_PROC || '(UPDATE_ERECTIONGAOP)';
                RETURN;
            END;
        END IF;

        O_APP_CODE := '0';
        O_APP_MSG := 'OK';

    EXCEPTION WHEN OTHERS THEN
        O_APP_CODE := '-1';
        O_APP_MSG  := V_PROC || SQLERRM;
        O_RESULT := 'F';
        O_HASCOPYSHIP := 'N';

    END COPY_SHIPTICKETDATA;
--------------------------------------------------------------------------------

--------------------------------------------------------------------------------
    PROCEDURE SELECT_FT_GAP2
--------------------------------------------------------------------------------
    (
        I_FIG_NO      IN VARCHAR2,
        I_FIG_SHP     IN VARCHAR2,
        I_CPY_FIG_NO  IN VARCHAR2,
        I_CPY_SHP     IN VARCHAR2,
        O_CUR  OUT SYS_REFCURSOR
    )
    IS

    BEGIN
        OPEN O_CUR FOR
        SELECT (fc_get_netday(A.LC) - fc_get_netday(A.KL) ) -(fc_get_netday(B.LC)- fc_get_netday(B.KL)) NO, A.FT FTA, B.FT FTB
        FROM TSAD001 A, TSAD001 B
        WHERE A.FIG_NO = I_FIG_NO
            AND B.FIG_NO = I_CPY_FIG_NO
            AND A.FIG_SHP = I_FIG_SHP
            AND B.FIG_SHP = I_CPY_SHP;

    END SELECT_FT_GAP2;
--------------------------------------------------------------------------------


--------------------------------------------------------------------------------
    PROCEDURE UPDATE_ERECTIONGAOP
--------------------------------------------------------------------------------
    (
        I_FIG_NO      IN VARCHAR2,
        I_FIG_SHP     IN VARCHAR2,
        I_CPY_FIG_NO  IN VARCHAR2,
        I_CPY_SHP     IN VARCHAR2,
        I_SHP_COD     IN VARCHAR2
    )
    IS
        V_CNT NUMBER := 0;
        V_GAP NUMBER := 0;
    BEGIN
--    //차이 = (원본 FT1 - KL) TSAD001 - (복사본 FT1 - KL) TSAA002
--    //탑재네트워크에서 FT 이후의 MIS를 구하고
--    //MIS 로 검색해서 시작일 종료일 업데이트
--    //TSEG005 PLN_ST PLN_FI

     SELECT COUNT(0) INTO V_CNT
        FROM TSAD001 A, TSAD001 B
        WHERE A.FIG_NO = I_FIG_NO
        AND B.FIG_NO = I_CPY_FIG_NO
        AND A.FIG_SHP = I_FIG_SHP
        AND B.FIG_SHP = I_CPY_SHP;

        IF V_CNT > 0 THEN
            --GetFTGap2
            SELECT NVL(CASE WHEN A.FT IS NOT NULL AND B.FT IS NOT NULL THEN
                            ((fc_get_netday(A.LC) - fc_get_netday(A.KL) ) -(fc_get_netday(B.LC)- fc_get_netday(B.KL)))
                        END, 0) GAP INTO V_GAP
            FROM TSAD001 A, TSAD001 B
            WHERE A.FIG_NO = I_FIG_NO
                AND B.FIG_NO = I_CPY_FIG_NO
                AND A.FIG_SHP = I_FIG_SHP
                AND B.FIG_SHP = I_CPY_SHP;

            IF V_GAP <> 0 THEN
                UPDATE --TSEG013
                    (SELECT R.FIG_NO, R.SHP_COD, R.PRE_NOD, R.AFT_NOD, R.STD_PCH
                        FROM TSEG012 E, TSEG013 R
                        WHERE E.FIG_NO = R.FIG_NO
                            AND E.SHP_COD = R.SHP_COD
                            AND R.FIG_NO = I_FIG_NO
                            AND R.SHP_COD = I_SHP_COD
                            AND R.AFT_NOD = E.MIS_NOD
                            AND E.BLK_LST ='L/C') --19.07.23 신성훈 PITCH조정 F/T에서 L/C로 기준 조정요청에 따른 수정
                SET STD_PCH = CASE WHEN (STD_PCH + V_GAP) > 0 THEN STD_PCH + V_GAP
                                   ELSE 0 END;
            END IF;
        END IF;

    END UPDATE_ERECTIONGAOP;
--------------------------------------------------------------------------------
--> 표준 부서적용 로직 시작
/******************************************************************************
   - 최종수정일 : 2021-06-16
   - 최종수정자 :
   - 기능 상세  : 표준 부서적용
******************************************************************************/
--------------------------------------------------------------------------------
    PROCEDURE STANDARD_DEPT
--------------------------------------------------------------------------------
    (
          ORESULT_CUR               OUT     SYS_REFCURSOR
        , IN_FIG_SHP                IN      VARCHAR2

    )
    IS

    BEGIN
        FOR RC IN
        (
             -- PKG_CPS009_MTS017.TSAA002_FIG_SHP_S1 에서 가져왔다.
             SELECT SHP_COD, SHP_DES, FIG_SHP, YD_GBN, DCK_COD,
                    STD_SHP, MAK_TM, SHP_KND, SHP_TYP, SHP_TYP_QTY,
                    SHP_TYP_NM, WC, KL, FT, LC,
                    DL, PNT_YN, BLD_COD, LOA, LBP,
                    WID, DEP, TOT_TON, CG_TON, ORI_OWN_NM,
                    ORI_NAT, SFIG_NO, FIG_NO, PLN_YN, SHP_GBN,
                    SHP_STS, REL_FIG_SHP, BD_STA, PSP_NR, IN_DAT,
                    UP_DAT, DE_DAT, IN_USR, UP_USR, WCT,
                    WCH, CPY_SHP, OWNRP_NM, NW_GEN_FLG, PRJ_GBN,
                    KL2, FT2, FT3, KL_DCK, KL2_DCK,
                    FT_DCK, FT2_DCK, FT3_DCK
                FROM TSAA002 -- 호선기본정보<중일정사용>
                WHERE FIG_SHP = IN_FIG_SHP
        )
        LOOP
        -- //2015.06.06 박병열 세부공종 000 ACT 만 조회 되도록 수정 (최준현 과장 요청)
--        RC.FIG_SHP, RC.FIG_NO
          NULL;

        END LOOP;


    END STANDARD_DEPT;
--------------------------------------------------------------------------------


END PKG_PP009_LTS004;
