CREATE OR REPLACE PROCEDURE PROC_COPY_PROJECT_DATA1_O (
   P_FIG_NO         TSAD001.FIG_NO%TYPE, -- PPING-NOW
   P_SHP_COD        TSAD001.SHP_COD%TYPE, -- 1046240
   P_FIG_SHP        TSAD001.FIG_SHP%TYPE, -- 2947
   P_CPY_FIG_NO     TSAD001.FIG_NO%TYPE, --999999999
   P_CPY_SHP_COD    TSAD001.SHP_COD%TYPE, -- 1046240
   P_CPY_FIG_SHP    TSAD001.FIG_SHP%TYPE, -- 2947
   P_CPY_DCK_COD    TSAD001.DCK_COD%TYPE, -- 2
   P_IN_USR         TSAD001.IN_USR%TYPE,  -- 입력자
   P_ACT_PLN_YN     VARCHAR2 := 'N',
   O_APP_MSG       OUT VARCHAR2   -- 오류 메세지
   )      -- Act.일정변경 Y/N
IS
/******************************************************************************

   NAME:       PROC_COPY_PROJECT_DATA1_TSMG2
   PURPOSE:    운영에서 사업계획(시뮬레이션영역)으로 복사(의장)

******************************************************************************/
   V_KL_GAP   NUMBER (8);
   V_ERR      VARCHAR2(4000);
   V_IS_CPY_SHP VARCHAR2(1) := 'N';
   NEW_P_CPY_SHP_COD VARCHAR2(10);
   V_CASE_NO  TSFN207.CASE_NO%TYPE;
   V_CNT  NUMBER(9);
BEGIN
  IF P_ACT_PLN_YN = 'Y' THEN
   SELECT fc_get_netday (A.KL) - fc_get_netday (B.KL)
     INTO V_KL_GAP
     FROM TSAD001 A, TSAD001 B
    WHERE A.FIG_NO = P_FIG_NO
      AND A.FIG_SHP = P_FIG_SHP
      AND B.FIG_NO = P_CPY_FIG_NO
      AND B.FIG_SHP = P_CPY_FIG_SHP;
  ELSE
   SELECT fc_get_netday (A.KL) - fc_get_netday (B.KL)
     INTO V_KL_GAP
     FROM TSAD001 A
        , TSAA002 B
    WHERE A.FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND A.FIG_SHP = P_FIG_SHP
      AND B.FIG_SHP = P_CPY_FIG_SHP;
  END IF;

   --운영에서 사업계획
   UPDATE TSAD001
      SET CPY_SHP1 = P_CPY_FIG_SHP, CPY_FIG_NO1 = '999999999'
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

   --의장중일정 (블록 의장)
   DELETE
     FROM TSMG_TSFA001
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

   BEGIN

    V_ERR := 'TSMG_TSFA001 생성';
   -- 의장중일정 (블록 의장)
   INSERT INTO TSMG_TSFA001 (/*+ parallel(4) */FIG_NO
                           , SHP_COD
                           , ACT_COD
                           , NWK_ID
                           , ACT_ID
                           , ACT_DES
                           , ACT_TYP
                           , PLN_ST
                           , PLN_FI
                           , EST_ST
                           , EST_FI
                           , STD_TRM
                           , PLN_TRM
                           , NET_TRM
                           , DPT_COD
                           , MHR_TOT
                           , MHR_STU -- 실투입공수
                           , MHR_SGI -- 실기성공수
                           , MHR_REL -- 실공수
                           , STD_MHR -- 표준공수
                           , EXP_MHR
                           , MHR_LOD
                           , SCV_TYP
                           , ITM_COD
                           , MIS_COD
                           , DIV_COD
                           , DCK_COD
                           , WRK_STG
                           , WRK_TYP
                           , WRK_TYP2
                           , PNT_SCH
                           , ST_OPT_PNT
                           , ST_JOE_PNT
                           , FI_JOE_PNT
                           , FI_OPT_PNT
                           , CPY_SHP
                           , QUAY_YN
                           , JOO_YN
                           , FIG_SHP
                           , IN_DAT
                           , IN_USR
                           , MHR_GBN
                           , HO_GUBUN
                           , POS_ID
                           , DPT_COD_OR
                           , UNI_STG
                           , STD_DPT_YN
                           , IP
                           , PID
                           , FIXED_YN
                           , DPT_FIXED_YN
                           , MANUAL_YN
                           , ERR_YN
                           , DEL_ERR_YN
                           , ACT_STATUS
                           , MUL_QTY
                           , MUL_QTY_O
                           , ZZOUT_CODE)
      WITH MH_RT AS
      (
        SELECT Z.FIG_SHP, Z.ACT_COD
             , NVL((SUM(ARBEI_ACTCOST) + SUM(ARBEI_ACTCOSTE) + SUM(ARBEI_SPTEDS) + SUM(ARBEI_SPTEDO)), 0) RT_STU -- 실투입 = 직영실투입 + 협력사실투입, ※실공수 = 표준 공수
             , NVL((SUM(ARBEI_BCWP)    + SUM(ARBEI_BCWPE)), 0) RT_SGI -- 기성 = 직영기성 + 협력사기성
          FROM TSMG027 Z,
               (SELECT NVL(RT3_1.RT_APPLY_YM, TO_CHAR(SYSDATE, 'YYYYMM')) || '99' RT_APPLY_YM FROM TSAC003 RT3_1 WHERE FIG_NO = P_FIG_NO) RT3
         WHERE Z.FIG_SHP = P_FIG_SHP
           AND Z.BUDAT <= RT3.RT_APPLY_YM
         GROUP BY Z.FIG_SHP, Z.ACT_COD
      )
      SELECT /*+ parallel(4) */P_FIG_NO FIG_NO,
             P_SHP_COD SHP_COD,
             A.ACT_COD,
             REPLACE (NWK_ID, P_CPY_FIG_SHP, P_FIG_SHP) NWK_ID,
             ACT_ID,
             ACT_DES,
             ACT_TYP,
             fc_get_calday (fc_get_netday (NVL(A.PLN_ST,A.EST_ST)) + V_KL_GAP),
             fc_get_calday (fc_get_netday (NVL(A.PLN_FI,A.EST_FI)) + V_KL_GAP),
             A.EST_ST,
             A.EST_FI,
             STD_TRM,
             PLN_TRM,
             NET_TRM,
             --NVL(C.RPLN_STG, A.DPT_COD) AS DPT_COD, -- TSEA002 의 부서 코드만 가져오게 수정. SSM, 박준 책임 요청 2022-07-11
             A.DPT_COD,
             MHR_TOT,
             --NVL(RT_STU, 0),  -- 실투입공수
             --H도크도장부인 경우, 선행도장 작업은 실투입=기성으로 처리 2022.07.05
             CASE WHEN NVL(C.RPLN_STG, A.DPT_COD) = 'C7J000'
                   AND SUBSTR(NVL(A.POS_ID,C.POSID), 5, 5) IN ('FNH32', 'FNHZ1')
                  THEN NVL(RT_SGI, 0)
                  ELSE NVL(RT_STU, 0)
             END,
             NVL(RT_SGI, 0),  -- 실기성공수
             --NVL(CASE WHEN ZZARBEI_PO IS NOT NULL THEN ZZARBEI_PO ELSE MHR_REL END, 0), -- 실공수
             --NVL(CASE WHEN ZZARBEI_PO IS NOT NULL THEN ZZARBEI_PO ELSE MHR_REL END, 0), -- 표준공수
             CASE WHEN (DPT_COD LIKE 'C59%' OR DPT_COD LIKE 'X1%') THEN 0 ELSE NVL(ZZARBEI_PO, 0) END, --외주는 0
             CASE WHEN (DPT_COD LIKE 'C59%' OR DPT_COD LIKE 'X1%') THEN 0 ELSE NVL(ZZARBEI_PO, 0) END, --외주는 0
             0,
             MHR_LOD,
             SCV_TYP,
             ITM_COD,
             MIS_COD,
             DIV_COD,
             P_CPY_DCK_COD DCK_COD,
             WRK_STG,
             WRK_TYP,
             WRK_TYP2,
             PNT_SCH,
             ST_OPT_PNT,
             ST_JOE_PNT,
             FI_JOE_PNT,
             FI_OPT_PNT,
             P_CPY_FIG_SHP CPY_SHP,
             QUAY_YN,
             JOO_YN,
             P_FIG_SHP FIG_SHP,
             TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
             P_IN_USR IN_USR,
             MHR_GBN,
             HO_GUBUN,
             REPLACE (NVL(A.POS_ID,C.POSID), P_CPY_FIG_SHP, P_FIG_SHP) POS_ID,
             DPT_COD_OR,
             UNI_STG,
             STD_DPT_YN,
             IP,
             PID,
             FIXED_YN,
             DPT_FIXED_YN,
             '' MANUAL_YN,
             '' ERR_YN,
             '' DEL_ERR_YN,
             '' ACT_STATUS,
             (SELECT X.MUL_QTY
                FROM TSEA001 X
               WHERE A.SHP_COD = X.SHP_COD(+)
                 AND A.ACT_COD = X.ACT_COD(+)
                 AND M.MUL_UNIT = X.LOD_COD(+)) MUL_QTY,
             (SELECT X.MUL_QTY
                FROM TSEA001 X
               WHERE A.SHP_COD = X.SHP_COD(+)
                 AND A.ACT_COD = X.ACT_COD(+)
                 AND M.MUL_UNIT_O = X.LOD_COD(+)) MUL_QTY_O,
             C.ZZOUT_CODE
        FROM TSFA001 A
           , C51A.T51A0030 C
           , MH_RT Z
           , TSMG033 M
       WHERE A.SHP_COD = P_CPY_SHP_COD
         --AND A.WRK_STG NOT IN ('P', 'Q', 'R')
         --AND A.WRK_STG || A.WRK_TYP NOT IN ('L32', 'L64', 'K64')  -- 해당 공정공종은 선각에서 사용(한승훈 협의완)
         AND A.FIG_SHP = Z.FIG_SHP(+)
         AND A.FIG_SHP = C.SHPNO(+)
         AND TRIM(A.ACT_COD) = C.ACT_NO(+)
         AND TRIM(A.ACT_COD) = Z.ACT_COD(+)
         AND SUBSTR(A.POS_ID, 5, 5) = M.WBS_ID(+)
         AND M.OUTFIT_INDC(+) = 'Y'
         AND A.ACT_TYP LIKE 'A%'
         AND A.ACT_COD NOT LIKE '4%'
         AND A.POS_ID IS NOT NULL
         AND A.ACT_COD NOT IN (      -- 이미 추가된 ACT.는 제외
                                    SELECT ACT_COD
                                      FROM TSEG005
                                     WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                                       AND SHP_COD = P_SHP_COD
                             )
--         AND NOT EXISTS (
--                         SELECT X.FIG_SHP
--                           FROM TSEG005 X
--                          WHERE X.FIG_NO  = P_FIG_NO
--                            AND X.SHP_COD = A.SHP_COD
--                            AND X.ACT_COD = A.ACT_COD
--                         )
         ;

        V_ERR := 'TSFA001에서 누락된 ACT 추가(1)P_FIG_NO='||P_FIG_NO||',P_SHP_COD='||P_SHP_COD||',P_CPY_SHP_COD='||P_CPY_SHP_COD||',P_CPY_FIG_SHP='||P_CPY_FIG_SHP||',이전 추가 ACT.갯수='||SQL%ROWCOUNT;
    -- TSFA001에서 누락된 ACT 추가
       INSERT INTO TSMG_TSFA001 (FIG_NO,
                            SHP_COD,
                            ACT_COD,
                            ITM_COD,
                            PLN_ST,
                            PLN_FI,
                            ACT_DES,
                            DPT_COD,
                            MHR_STU, -- 실투입공수
                            MHR_SGI, -- 실기성공수
                            MHR_REL, -- 실공수
                            STD_MHR, -- 표준공수
                            DCK_COD,
                            WRK_TYP,
                            WRK_STG,
                            WRK_TYP2,
                            CPY_SHP,
                            FIG_SHP,
                            IN_DAT,
                            IN_USR,
                            MUL_QTY,
                            MUL_QTY_O,
                            PLN_ST_INI,
                            PLN_FI_INI,
                            ZZOUT_CODE,
                            POS_ID,
                            ACT_TYP)
          WITH MH_RT AS
          (
            SELECT Z.FIG_SHP, Z.ACT_COD
                 , NVL((SUM(ARBEI_ACTCOST) + SUM(ARBEI_ACTCOSTE) + SUM(ARBEI_SPTEDS) + SUM(ARBEI_SPTEDO)), 0) RT_STU -- 실투입 = 직영실투입 + 협력사실투입, ※실공수 = 표준 공수
                 , NVL((SUM(ARBEI_BCWP)    + SUM(ARBEI_BCWPE)), 0) RT_SGI -- 기성 = 직영기성 + 협력사기성
              FROM TSMG027 Z,
                   (SELECT NVL(RT3_1.RT_APPLY_YM, TO_CHAR(SYSDATE, 'YYYYMM')) || '99' RT_APPLY_YM FROM TSAC003 RT3_1 WHERE FIG_NO = P_FIG_NO) RT3
             WHERE Z.FIG_SHP = P_FIG_SHP
               AND Z.BUDAT <= RT3.RT_APPLY_YM
             GROUP BY Z.FIG_SHP, Z.ACT_COD
          )
          SELECT P_FIG_NO FIG_NO,
                 P_SHP_COD SHP_COD,
                 A.ACT_NO,
                 LPAD(A.ACT_NO, 5),
                 --A.RPLN_SDTE,
                 --A.RPLN_FDTE,
                 fc_get_calday (fc_get_netday (RPLN_SDTE) + V_KL_GAP),
                 fc_get_calday (fc_get_netday (RPLN_FDTE) + V_KL_GAP),
                 A.LTXA1,
                 RPLN_STG,
                 --NVL(RT_STU, 0),  -- 실투입공수
                 --H도크도장부인 경우, 선행도장 작업은 실투입=기성으로 처리 2022.07.05
                 CASE WHEN A.RPLN_STG = 'C7J000'
                       AND SUBSTR(A.POSID, 5, 5) IN ('FNH32', 'FNHZ1')
                      THEN NVL(RT_SGI, 0)
                      ELSE NVL(RT_STU, 0)
                 END,
                 NVL(RT_SGI, 0), -- 실기성공수
                 --NVL(CASE WHEN RPLN_STG LIKE 'C59%' THEN 0 ELSE A.ZZARBEI_PO END, 0), -- 실공수
                 --NVL(CASE WHEN RPLN_STG LIKE 'C59%' THEN 0 ELSE A.ZZARBEI_PO END, 0), -- 표준공수
                 CASE WHEN (RPLN_STG LIKE 'C59%' OR RPLN_STG LIKE 'X1%') THEN 0 ELSE NVL(A.ZZARBEI_PO,0) END, -- 표준공수
                 CASE WHEN (RPLN_STG LIKE 'C59%' OR RPLN_STG LIKE 'X1%') THEN 0 ELSE NVL(A.ZZARBEI_PO,0) END, -- 표준공수
                 P_CPY_DCK_COD DCK_COD,
                 A.PJTKND WRK_TYP,
                 A.PRO WRK_STG,
                 A.PRO||A.PJTKND WRK_TYP2,
                 P_CPY_FIG_SHP CPY_SHP,
                 P_FIG_SHP FIG_SHP,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR,
                 (SELECT NVL(X.MUL_QTY, 0)
                    FROM TSEA001 X
                   WHERE A.SHPNO = X.FIG_SHP(+)
                     AND RPAD(A.ACT_NO, 13, ' ') = X.ACT_COD(+)
                     AND M.MUL_UNIT = X.LOD_COD(+)) MUL_WGT,
                 (SELECT NVL(X.MUL_QTY, 0)
                    FROM TSEA001 X
                   WHERE A.SHPNO = X.FIG_SHP(+)
                     AND RPAD(A.ACT_NO, 13, ' ') = X.ACT_COD(+)
                     AND M.MUL_UNIT_O = X.LOD_COD(+)) MUL_WGT_O,
                 fc_get_calday (fc_get_netday (RPLN_SDTE) + V_KL_GAP),
                 fc_get_calday (fc_get_netday (RPLN_FDTE) + V_KL_GAP),
                 A.ZZOUT_CODE,
                 REPLACE (A.POSID, P_CPY_FIG_SHP, P_FIG_SHP) POS_ID,
                 A.ZZACT_ATTRIB
            FROM (
                    SELECT *
                      FROM C51A.T51A0030
                     WHERE PRO||PJTKND IN
                           (
                            SELECT WRK_STG||WRK_TYP
                              FROM TSEE002
                             WHERE HO_GUBUN = '2'
                               AND PRJ_GBN = 'C000'
                             GROUP BY WRK_STG||WRK_TYP
                           )
                      AND SHPNO = P_CPY_FIG_SHP
                      AND ZZACT_ATTRIB LIKE 'A%'
                      --AND ZZACT_ATTRIB IN ('A02', 'B01', 'B02', 'B03', 'C01', 'C02', 'E01', 'E02', 'E03')
                      AND ACT_NO NOT LIKE '4%'
                 ) A
               , MH_RT Z
               , TSMG033 M
           WHERE A.SHPNO = P_CPY_FIG_SHP
             AND A.SHPNO = Z.FIG_SHP(+)
             AND A.ACT_NO = Z.ACT_COD(+)
             AND A.ACT_NO NOT IN (  -- 이미 추가된 ACT는 제외
                                    SELECT TRIM(ACT_COD)
                                      FROM TSMG_TSFA001
                                     WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                                       AND SHP_COD = P_SHP_COD  --P_CPY_FIG_SHP
                                 )
             AND A.ACT_NO NOT IN ( -- 이미 추가된 ACT.는 제외
                                    SELECT TRIM(ACT_COD)
                                      FROM TSEG005
                                     WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                                       AND SHP_COD = P_SHP_COD
                                 )
--             AND NOT EXISTS (SELECT X.ACT_COD
--                               FROM TSMG_TSFA001 X
--                              WHERE X.FIG_NO  = P_FIG_NO
--                                AND X.SHP_COD = P_SHP_COD --P_CPY_SHP_COD
--                                AND X.ACT_COD = RPAD(A.ACT_NO,13,' ')
--                            )
--             AND NOT EXISTS (SELECT X.FIG_SHP
--                               FROM TSEG005 X
--                              WHERE X.FIG_NO  = P_FIG_NO
--                                AND X.SHP_COD = P_SHP_COD
--                                AND X.ACT_COD = RPAD(A.ACT_NO,13,' ')
--		                        )
             AND SUBSTR(A.POSID, 5, 5) = M.WBS_ID(+);
--             AND M.OUTFIT_INDC(+) = 'Y';


   EXCEPTION
     WHEN OTHERS THEN
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_346 : '||V_ERR||','||SQLERRM || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

   -- 원단위 적용 UPDATE
--   BEGIN
--   UPDATE TSMG_TSFA001
--      SET UNIT_GB = CASE WHEN STD_MHR > 0 AND MUL_QTY > 0 THEN 'A'
--                         WHEN STD_MHR > 0 AND MUL_QTY = 0 THEN 'G'
--                         ELSE '' END
--        , UNIT    = CASE WHEN STD_MHR > 0 AND MUL_QTY > 0 THEN MUL_QTY / STD_MHR
--                         ELSE 0 END
--    WHERE FIG_NO = P_FIG_NO
--      AND SHP_COD = P_SHP_COD;
--   EXCEPTION
--     WHEN OTHERS THEN
--       V_ERR := 'ERR1:'||SQLERRM;
--       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_UNIT_GB : '||V_ERR;
--       RETURN;
--     END;

   -- 원단위(그룹평균)
--   BEGIN
--   UPDATE TSMG_TSFA001 A
--      SET UNIT    = (SELECT SUM(Z.MUL_QTY) / SUM(Z.STD_MHR)
--                       FROM TSMG_TSFA001 Z
--                      WHERE Z.FIG_NO = A.FIG_NO
--                        AND Z.SHP_COD = A.SHP_COD
--                        AND Z.POS_ID = A.POS_ID)
--    WHERE A.FIG_NO = P_FIG_NO
--      AND A.SHP_COD = P_SHP_COD
--      AND A.UNIT_GB = 'G';
--   EXCEPTION
--     WHEN OTHERS THEN
--       V_ERR := 'ERR1:'||SQLERRM;
--       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_UNIT : '||V_ERR;
--       RETURN;
--     END;

   --의장중일정 (구획 의장)
   DELETE
     FROM TSMG_TSFN101
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

    V_ERR := '의장중일정 (구획 의장)생성';
   BEGIN
   INSERT INTO TSMG_TSFN101 (/*+ parallel(4) */FIG_NO
                           , CASE_NO
                           , FIG_SHP
                           , ACT_COD
                           , SHP_COD
                           , NWK_ID
                           , ACT_ID
                           , ACT_DES
                           , ACT_TYP
                           , PLN_ST
                           , PLN_FI
                           , EST_ST
                           , EST_FI
                           , STD_TRM
                           , PLN_TRM
                           , NET_TRM
                           , DPT_COD
                           , MHR_TOT
                           , MHR_STU -- 실투입공수
                           , MHR_SGI -- 실기성공수
                           , MHR_REL -- 실공수
                           , STD_MHR -- 표준공수
                           , EXP_MHR
                           , MHR_LOD
                           , SCV_TYP
                           , ITM_COD
                           , MIS_COD
                           , DIV_COD
                           , DCK_COD
                           , WRK_STG
                           , WRK_TYP
                           , WRK_TYP2
                           , PNT_SCH
                           , ST_OPT_PNT
                           , ST_JOE_PNT
                           , FI_JOE_PNT
                           , FI_OPT_PNT
                           , CPY_SHP
                           , QUAY_YN
                           , JOO_YN
                           , MHR_GBN
                           , HO_GUBUN
                           , POS_ID
                           , DPT_COD_OR
                           , UNI_STG
                           , STD_DPT_YN
                           , FIXED_YN
                           , DPT_FIXED_YN
                           , IP
                           , PID
                           , IN_DAT
                           , IN_USR
                           , VIEW_SEQ
                           , SEALED_YN
                           , MUL_QTY
                           , MUL_QTY_O
                           , ZZOUT_CODE)
      WITH MH_RT AS
          (
            SELECT Z.FIG_SHP, Z.ACT_COD
                 , NVL((SUM(ARBEI_ACTCOST) + SUM(ARBEI_ACTCOSTE) + SUM(ARBEI_SPTEDS) + SUM(ARBEI_SPTEDO)), 0) RT_STU -- 실투입 = 직영실투입 + 협력사실투입, ※실공수 = 표준 공수
                 , NVL((SUM(ARBEI_BCWP)    + SUM(ARBEI_BCWPE)), 0) RT_SGI -- 기성 = 직영기성 + 협력사기성
              FROM TSMG027 Z,
                   (SELECT NVL(RT3_1.RT_APPLY_YM, TO_CHAR(SYSDATE, 'YYYYMM')) || '99' RT_APPLY_YM FROM TSAC003 RT3_1 WHERE FIG_NO = P_FIG_NO) RT3
             WHERE Z.FIG_SHP = P_FIG_SHP
               AND Z.BUDAT <= RT3.RT_APPLY_YM
             GROUP BY Z.FIG_SHP, Z.ACT_COD
          )
      SELECT /*+ parallel(4) */P_FIG_NO FIG_NO,
             CASE_NO,
             P_FIG_SHP FIG_SHP,
             A.ACT_COD,
             P_SHP_COD SHP_COD,
             REPLACE (NWK_ID, P_CPY_FIG_SHP, P_FIG_SHP) NWK_ID,
             ACT_ID,
             ACT_DES,
             ACT_TYP,
             fc_get_calday (fc_get_netday (NVL(A.PLN_ST,A.EST_ST)) + V_KL_GAP),
             fc_get_calday (fc_get_netday (NVL(A.PLN_FI,A.EST_FI)) + V_KL_GAP),
             A.EST_ST,
             A.EST_FI,
             STD_TRM,
             PLN_TRM,
             NET_TRM,
             --NVL(C.RPLN_STG, A.DPT_COD) AS DPT_COD, -- TSEA002 의 부서 코드만 가져오게 수정. SSM, 박준 책임 요청 2022-07-11
             A.DPT_COD,
             MHR_TOT,
             --NVL(RT_STU, 0),  -- 실투입공수
             --H도크도장부인 경우, 선행도장 작업은 실투입=기성으로 처리 2022.07.05
             CASE WHEN NVL(C.RPLN_STG, A.DPT_COD) = 'C7J000'
                   AND SUBSTR(NVL(A.POS_ID,C.POSID), 5, 5) IN ('FNH32', 'FNHZ1')
                  THEN NVL(RT_SGI, 0)
                  ELSE NVL(RT_STU, 0)
             END,
             NVL(RT_SGI, 0),  -- 실기성공수
             --NVL(CASE WHEN DPT_COD LIKE 'C59%' THEN 0 ELSE CASE WHEN ZZARBEI_PO IS NOT NULL THEN ZZARBEI_PO ELSE MHR_REL END END, 0), -- 실공수  (포항공장부는 0)
             --NVL(CASE WHEN DPT_COD LIKE 'C59%' THEN 0 ELSE CASE WHEN ZZARBEI_PO IS NOT NULL THEN ZZARBEI_PO ELSE MHR_REL END END, 0), -- 표준공수 (포항공장부는 0)
             CASE WHEN (DPT_COD LIKE 'C59%' OR DPT_COD LIKE 'X1%') THEN 0 ELSE NVL(ZZARBEI_PO, 0) END, --외주는 0
             CASE WHEN (DPT_COD LIKE 'C59%' OR DPT_COD LIKE 'X1%') THEN 0 ELSE NVL(ZZARBEI_PO, 0) END, --외주는 0
             0,
             MHR_LOD,
             SCV_TYP,
             ITM_COD,
             MIS_COD,
             DIV_COD,
             P_CPY_DCK_COD DCK_COD,
             WRK_STG,
             WRK_TYP,
             WRK_TYP2,
             PNT_SCH,
             ST_OPT_PNT,
             ST_JOE_PNT,
             FI_JOE_PNT,
             FI_OPT_PNT,
             P_CPY_FIG_SHP CPY_SHP,
             QUAY_YN,
             JOO_YN,
             MHR_GBN,
             HO_GUBUN,
             REPLACE (NVL(A.POS_ID,C.POSID), P_CPY_FIG_SHP, P_FIG_SHP) POS_ID,
             DPT_COD_OR,
             UNI_STG,
             STD_DPT_YN,
             FIXED_YN,
             DPT_FIXED_YN,
             IP,
             PID,
             TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
             P_IN_USR IN_USR,
             VIEW_SEQ,
             SEALED_YN,
             (SELECT X.MUL_QTY
                FROM TSEA001 X
               WHERE A.SHP_COD = X.SHP_COD(+)
                 AND A.ACT_COD = X.ACT_COD(+)
                 AND M.MUL_UNIT = X.LOD_COD(+)) MUL_QTY,
             (SELECT X.MUL_QTY
                FROM TSEA001 X
               WHERE A.SHP_COD = X.SHP_COD(+)
                 AND A.ACT_COD = X.ACT_COD(+)
                 AND M.MUL_UNIT_O = X.LOD_COD(+)) MUL_QTY_O,
             C.ZZOUT_CODE
        FROM TSFN101 A
           , C51A.T51A0030 C
           , MH_RT Z
           , TSMG033 M
       WHERE A.CASE_NO = '000000000000'
         AND A.SHP_COD = P_CPY_SHP_COD
         --AND A.WRK_STG || A.WRK_TYP NOT IN ('L32', 'L64', 'K64')  -- 해당 공정공종은 선각에서 사용(한승훈 협의완)
         AND A.FIG_SHP = Z.FIG_SHP(+)
         AND A.FIG_SHP = C.SHPNO(+)
         AND A.ACT_COD = C.ACT_NO(+)
         AND TRIM(A.ACT_COD) = Z.ACT_COD(+)
         AND SUBSTR(A.POS_ID, 5, 5) = M.WBS_ID(+)
         AND M.OUTFIT_INDC(+) = 'Y'
         AND A.ACT_TYP LIKE 'A%'
         --AND A.POS_ID IS NOT NULL
         --AND ( A.ACT_TYP LIKE 'A%' OR
         --      A.ACT_TYP LIKE 'B%' OR
         --      A.ACT_TYP LIKE 'C%' )
         ;

        V_ERR := 'TSFN101에서 누락된 ACT 추가(2)P_FIG_NO='||P_FIG_NO||',P_SHP_COD='||P_SHP_COD||',P_CPY_SHP_COD='||P_CPY_SHP_COD||',P_CPY_FIG_SHP='||P_CPY_FIG_SHP||',이전 추가 ACT.갯수='||SQL%ROWCOUNT;
    -- TSFN101에서 누락된 ACT 추가
       INSERT INTO TSMG_TSFN101 (FIG_NO,
                            SHP_COD,
                            ACT_COD,
                            ITM_COD,
                            PLN_ST,
                            PLN_FI,
                            ACT_DES,
                            DPT_COD,
                            MHR_STU, -- 실투입공수
                            MHR_SGI, -- 실기성공수
                            MHR_REL, -- 실공수
                            STD_MHR, -- 표준공수
                            DCK_COD,
                            WRK_TYP,
                            WRK_STG,
                            WRK_TYP2,
                            CPY_SHP,
                            FIG_SHP,
                            IN_DAT,
                            IN_USR,
                            MUL_QTY,
                            MUL_QTY_O,
                            PLN_ST_INI,
                            PLN_FI_INI,
                            ZZOUT_CODE,
                            POS_ID,
                            ACT_TYP)
          WITH MH_RT AS
          (
            SELECT Z.FIG_SHP, Z.ACT_COD
                 , NVL((SUM(ARBEI_ACTCOST) + SUM(ARBEI_ACTCOSTE) + SUM(ARBEI_SPTEDS) + SUM(ARBEI_SPTEDO)), 0) RT_STU -- 실투입 = 직영실투입 + 협력사실투입, ※실공수 = 표준 공수
                 , NVL((SUM(ARBEI_BCWP)    + SUM(ARBEI_BCWPE)), 0) RT_SGI -- 기성 = 직영기성 + 협력사기성
              FROM TSMG027 Z,
                   (SELECT NVL(RT3_1.RT_APPLY_YM, TO_CHAR(SYSDATE, 'YYYYMM')) || '99' RT_APPLY_YM FROM TSAC003 RT3_1 WHERE FIG_NO = P_FIG_NO) RT3
             WHERE Z.FIG_SHP = P_FIG_SHP
               AND Z.BUDAT <= RT3.RT_APPLY_YM
             GROUP BY Z.FIG_SHP, Z.ACT_COD
          )
          SELECT P_FIG_NO FIG_NO,
                 P_SHP_COD SHP_COD,
                 A.ACT_NO,
                 LPAD(A.ACT_NO, 5),
                 --A.RPLN_SDTE,
                 --A.RPLN_FDTE,
                 fc_get_calday (fc_get_netday (RPLN_SDTE) + V_KL_GAP),
                 fc_get_calday (fc_get_netday (RPLN_FDTE) + V_KL_GAP),
                 A.LTXA1,
                 RPLN_STG,
                 --NVL(RT_STU, 0),  -- 실투입공수
                 --H도크도장부인 경우, 선행도장 작업은 실투입=기성으로 처리 2022.07.05
                 CASE WHEN A.RPLN_STG = 'C7J000'
                       AND SUBSTR(A.POSID, 5, 5) IN ('FNH32', 'FNHZ1')
                      THEN NVL(RT_SGI, 0)
                      ELSE NVL(RT_STU, 0)
                 END,
                 NVL(RT_SGI, 0), -- 실기성공수
                 --NVL(CASE WHEN RPLN_STG LIKE 'C59%' THEN 0 ELSE A.ZZARBEI_PO END, 0), -- 실공수
                 --NVL(CASE WHEN RPLN_STG LIKE 'C59%' THEN 0 ELSE A.ZZARBEI_PO END, 0), -- 표준공수
                 CASE WHEN (RPLN_STG LIKE 'C59%' OR RPLN_STG LIKE 'X1%') THEN 0 ELSE NVL(A.ZZARBEI_PO,0) END, -- 표준공수
                 CASE WHEN (RPLN_STG LIKE 'C59%' OR RPLN_STG LIKE 'X1%') THEN 0 ELSE NVL(A.ZZARBEI_PO,0) END, -- 표준공수
                 P_CPY_DCK_COD DCK_COD,
                 A.PJTKND WRK_TYP,
                 A.PRO WRK_STG,
                 A.UNTPJT WRK_TYP2,
                 P_CPY_FIG_SHP CPY_SHP,
                 P_FIG_SHP FIG_SHP,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR,
                 (SELECT NVL(X.MUL_QTY, 0)
                    FROM TSEA001 X
                   WHERE A.SHPNO = X.FIG_SHP(+)
                     AND RPAD(A.ACT_NO, 13, ' ') = X.ACT_COD(+)
                     AND M.MUL_UNIT = X.LOD_COD(+)) MUL_WGT,
                 (SELECT NVL(X.MUL_QTY, 0)
                    FROM TSEA001 X
                   WHERE A.SHPNO = X.FIG_SHP(+)
                     AND RPAD(A.ACT_NO, 13, ' ') = X.ACT_COD(+)
                     AND M.MUL_UNIT_O = X.LOD_COD(+)) MUL_WGT_O,
                 fc_get_calday (fc_get_netday (RPLN_SDTE) + V_KL_GAP),
                 fc_get_calday (fc_get_netday (RPLN_FDTE) + V_KL_GAP),
                 A.ZZOUT_CODE,
                 REPLACE (A.POSID, P_CPY_FIG_SHP, P_FIG_SHP) POS_ID,
                 A.ZZACT_ATTRIB
            FROM (
                    SELECT *
                      FROM C51A.T51A0030
                     WHERE 1=1
                       AND ITEM LIKE '4%' --(ITEM LIKE '4%'  AND PJTKND IN ('4C', '4D') AND UNTPJT = '000') OR (ITEM LIKE '42%' AND PJTKND IN ('43', '4C', '4D') AND UNTPJT <> '000')
                       AND SHPNO = P_CPY_FIG_SHP
                       AND ZZACT_ATTRIB LIKE 'A%'
                       --AND ZZACT_ATTRIB IN ('A02', 'B01', 'B02', 'B03', 'C01', 'C02', 'E01', 'E02', 'E03')
                 ) A
               , MH_RT Z
               , TSMG033 M
           WHERE A.SHPNO = P_CPY_FIG_SHP
             AND A.SHPNO = Z.FIG_SHP(+)
             AND TRIM(A.ACT_NO) = Z.ACT_COD(+)
             AND A.ACT_NO NOT IN (  -- 이미 추가된 ACT는 제외
                                    SELECT TRIM(ACT_COD)
                                      FROM TSMG_TSFN101
                                     WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                                       AND SHP_COD = P_SHP_COD --P_CPY_FIG_SHP
                                 )
--             AND NOT EXISTS (SELECT X.ACT_COD
--                               FROM TSMG_TSFN101 X
--                              WHERE X.FIG_NO  = P_FIG_NO
--                                AND X.SHP_COD = P_SHP_COD--P_CPY_SHP_COD
--                                AND X.ACT_COD = RPAD(A.ACT_NO, 13, ' '))
             AND SUBSTR(A.POSID, 5, 5) = M.WBS_ID(+);
--             AND M.OUTFIT_INDC(+) = 'Y';

   EXCEPTION
     WHEN OTHERS THEN
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_651 : '||V_ERR||','||SQLERRM || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

   -- 원단위 적용 UPDATE
--   BEGIN
--   UPDATE TSMG_TSFN101
--      SET UNIT_GB = CASE WHEN STD_MHR > 0 AND MUL_QTY > 0 THEN 'A'
--                         WHEN STD_MHR > 0 AND MUL_QTY = 0 THEN 'G'
--                         ELSE '' END
--        , UNIT    = CASE WHEN STD_MHR > 0 AND MUL_QTY > 0 THEN MUL_QTY / STD_MHR
--                         ELSE 0 END
--    WHERE FIG_NO = P_FIG_NO
--      AND SHP_COD = P_SHP_COD;
--   EXCEPTION
--     WHEN OTHERS THEN
--       V_ERR := 'ERR2:'||SQLERRM;
--       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_UNIT_GB : '||V_ERR;
--       RETURN;
--     END;

   -- 원단위(그룹평균)
--   BEGIN
--   UPDATE TSMG_TSFN101 A
--      SET UNIT    = (SELECT SUM(Z.MUL_QTY) / SUM(Z.STD_MHR)
--                       FROM TSMG_TSFN101 Z
--                      WHERE Z.FIG_NO = A.FIG_NO
--                        AND Z.SHP_COD = A.SHP_COD
--                        AND Z.POS_ID = A.POS_ID)
--    WHERE A.FIG_NO = P_FIG_NO
--      AND A.SHP_COD = P_SHP_COD
--      AND A.UNIT_GB = 'G';
--   EXCEPTION
--     WHEN OTHERS THEN
--       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_UNIT : '||V_ERR||','||SQLERRM;
--       RETURN;
--     END;

   --[운영] 호선별 절점
   DELETE
     FROM TSMG_TSDC002
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

   BEGIN
   INSERT INTO TSMG_TSDC002 (/*+ parallel(4) */FIG_NO
                           , SHP_COD
                           , WRK_PNT
                           , PLN_ST
                           , PLN_FI
                           , PLN_TRM_NET
                           , PLN_TRM_CAL
                           , NET_ST
                           , NET_FI
                           , ST_JOE_PNT
                           , ST_CDT_TM
                           , FI_JOE_PNT
                           , FINI_CDT_TM
                           , FIG_SHP
                           , IN_DAT
                           , IN_USR
                           , DELAY_TYP)
      SELECT /*+ parallel(4) */P_FIG_NO FIG_NO
           , P_SHP_COD SHP_COD
           , WRK_PNT
           , fc_get_calday (fc_get_netday (PLN_ST) + V_KL_GAP)
           , fc_get_calday (fc_get_netday (PLN_FI) + V_KL_GAP)
           , PLN_TRM_NET
           , PLN_TRM_CAL
           , NET_ST
           , NET_FI
           , ST_JOE_PNT
           , ST_CDT_TM
           , FI_JOE_PNT
           , FINI_CDT_TM
           , P_FIG_SHP FIG_SHP
           , TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT
           , P_IN_USR IN_USR
           , DELAY_TYP
        FROM TSDC002
       WHERE SHP_COD = P_CPY_SHP_COD;
   EXCEPTION
     WHEN OTHERS THEN
       V_ERR := 'ERR3:'||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_733 : '||V_ERR || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

   --CASE 호선별 구획 품목 매핑관리
   DELETE
     FROM TSMG_TSFN021
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND FIG_SHP = P_FIG_SHP;

   BEGIN
   INSERT INTO TSMG_TSFN021 (/*+ parallel(4) */FIG_NO
                           , CASE_NO
                           , FIG_SHP
                           , DIV_COD
                           , ITM_COD
                           , MIS_COD
                           , DIV_LVL
                           , DIV_DES
                           , UPP_DIV
                           , MIS_ST
                           , MIS_FI
                           , SHP_COD
                           , DIV_OFFSET
                           , BLK_MIS_IND
                           , DIV_BLK_IND
                           , SEALED_YN
                           , IP
                           , PID
                           , IN_DAT
                           , IN_USR
                           , DIV_COD2)
      SELECT /*+ parallel(4) */P_FIG_NO FIG_NO
           , CASE_NO
           , P_FIG_SHP FIG_SHP
           , DIV_COD
           , ITM_COD
           , MIS_COD
           , DIV_LVL
           , DIV_DES
           , UPP_DIV
           , MIS_ST
           , MIS_FI
           , P_SHP_COD SHP_COD
           , DIV_OFFSET
           , BLK_MIS_IND
           , DIV_BLK_IND
           , SEALED_YN
           , IP
           , PID
           , TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT
           , P_IN_USR IN_USR
           , DIV_COD2
        FROM TSFN021
       WHERE CASE_NO = '000000000000'
         AND FIG_SHP = P_CPY_FIG_SHP; -- NO DATA_FOUND
   EXCEPTION
     WHEN OTHERS THEN
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_789 : '||V_ERR||','||SQLERRM || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

   --PE 골리앗 크레인 작업시간
   DELETE
     FROM TSMG_TSFN207
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

   --PE장 부하현황 화면에서 TSMG_TSFN207 테이블 사용 안 함 2022.01.27
--   BEGIN
--
--      NEW_P_CPY_SHP_COD := '';
--      V_IS_CPY_SHP := 'N';
--      V_CASE_NO := '000000000000';
--
--      -- 복사 대상 호선(P_CPY_SHP_COD)의 PE 정보가 없는 경우
--      SELECT CASE WHEN COUNT(*) = 0 THEN 'Y' ELSE 'N' END
--        INTO V_IS_CPY_SHP
--        FROM TSFN207 A -- PE 골리앗 크레인 작업시간
--       WHERE SHP_COD = P_CPY_SHP_COD
--         --AND CASE_NO = '000000000000'
--         ;
--
--       -- P_CPY_SHP_COD 호선의 PE 정보가 없는 경우
--       -- 복사 호선을 찾아서 복사호선을 기준으로 PE 정보를 생성한다.
--       IF V_IS_CPY_SHP = 'Y' THEN
--
--            SELECT CPY_SHP
--              INTO NEW_P_CPY_SHP_COD
--              FROM TSAD001
--             WHERE SHP_COD = P_CPY_SHP_COD
--               AND FIG_NO  = P_FIG_NO;
--
--            BEGIN
--
--                -- FIG_SHP로 SHP_COD를 구한다.
--                SELECT SHP_COD
--                  INTO NEW_P_CPY_SHP_COD
--                  FROM TSAD001
--                 WHERE FIG_SHP = NEW_P_CPY_SHP_COD
--                   AND FIG_NO  = P_FIG_NO;
--
--            EXCEPTION WHEN OTHERS THEN
--               O_APP_MSG := 'NEW_P_CPY_SHP_COD='||NEW_P_CPY_SHP_COD||',P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD;
--               RETURN;
--            END;
--
--       END IF;
--
--        -- CASE_NO 구하기, '000000000000' 확정된 번호가 없는 경우 MAX(CASE_NO)를 사용
--        SELECT COUNT(*)
--          INTO V_CNT
--          FROM TSFN207 A
--         WHERE CASE_NO = '000000000000'
--           AND SHP_COD = CASE WHEN NEW_P_CPY_SHP_COD IS NULL THEN P_CPY_SHP_COD ELSE NEW_P_CPY_SHP_COD END;
--
--        -- MAX(CASE_NO) 구하기
--        IF V_CNT = 0 THEN
--            SELECT MAX(CASE_NO)
--              INTO V_CASE_NO
--              FROM TSFN207 A
--             WHERE SHP_COD = CASE WHEN NEW_P_CPY_SHP_COD IS NULL THEN P_CPY_SHP_COD ELSE NEW_P_CPY_SHP_COD END;
--        END IF;
--
--
--       INSERT INTO TSMG_TSFN207 (/*+ parallel(4) */FIG_NO
--                               , CASE_NO
--                               , FIG_SHP
--                               , LOW_BLK
--                               , ITM_COD
--                               , ACT_COD
--                               , N41_ACT_COD
--                               , SHP_COD
--                               , ITM_DES
--                               , LAYER
--                               , SEQ_NO
--                               , DECK
--                               , PE_AREA
--                               , PE_TYPE
--                               , LOW_SIZE
--                               , ITM_SIZE
--                               , CRANE1
--                               , CRANE1_CNT
--                               , CRANE2
--                               , CRANE2_CNT
--                               , CRANE3
--                               , CRANE3_CNT
--                               , BLK_PE_OST_MID
--                               , BLK_PE_OST_PS
--                               , BLK_PE_OST_PP
--                               , L41_ST
--                               , N41_ST
--                               , L41_N41_OST
--                               , MID_ST
--                               , PLN_ST
--                               , EST_ST
--                               , RST_ST
--                               , LOW_MUL_WGT
--                               , ITM_MUL_WGT
--                               , GC_PE_TIME
--                               , TRANS_PE_TIME
--                               , ETC_SUPP_TIME
--                               , USE_YN
--                               , RMK
--                               , IP
--                               , PID
--                               , IN_DAT
--                               , IN_USR
--                               , PE_AREA_ST
--                               , PE_AREA_FI
--                               , PE_AREA_VOL
--                               , SHP_REV
--                               , IO_DIV
--                               , GC_GBN)
--          SELECT /*+ parallel(4) */P_FIG_NO FIG_NO
--               , '000000000000'-- CASE_NO
--               , P_FIG_SHP FIG_SHP
--               , LOW_BLK
--               , ITM_COD
--               , ACT_COD
--               , N41_ACT_COD
--               , P_SHP_COD SHP_COD
--               , ITM_DES
--               , LAYER
--               , SEQ_NO
--               , DECK
--               , PE_AREA
--               , PE_TYPE
--               , LOW_SIZE
--               , ITM_SIZE
--               , CRANE1
--               , CRANE1_CNT
--               , CRANE2
--               , CRANE2_CNT
--               , CRANE3
--               , CRANE3_CNT
--               , BLK_PE_OST_MID
--               , BLK_PE_OST_PS
--               , BLK_PE_OST_PP
--               , fc_get_calday (fc_get_netday (L41_ST) + V_KL_GAP) L41_ST
--               , fc_get_calday (fc_get_netday (N41_ST) + V_KL_GAP) N41_ST
--               , L41_N41_OST
--               , fc_get_calday (fc_get_netday (MID_ST) + V_KL_GAP) MID_ST
--               , fc_get_calday (fc_get_netday (PLN_ST) + V_KL_GAP) PLN_ST
--               , '' EST_ST
--               , '' RST_ST
--               , LOW_MUL_WGT
--               , ITM_MUL_WGT
--               , GC_PE_TIME
--               , TRANS_PE_TIME
--               , ETC_SUPP_TIME
--               , USE_YN
--               , RMK
--               , IP
--               , PID
--               , TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT
--               , P_IN_USR IN_USR
--               , fc_get_calday (fc_get_netday (PE_AREA_ST) + V_KL_GAP) PE_AREA_ST
--               , fc_get_calday (fc_get_netday (PE_AREA_FI) + V_KL_GAP) PE_AREA_FI
--               , PE_AREA_VOL
--               , SHP_REV
--               , IO_DIV
--               , CASE WHEN TRIM(PE_AREA) IS NULL THEN 'O' ELSE 'I' END GC_GBN
--            FROM TSFN207 A
--           WHERE CASE_NO = V_CASE_NO--'000000000000'
--             AND SHP_COD = CASE WHEN NEW_P_CPY_SHP_COD IS NULL THEN P_CPY_SHP_COD ELSE NEW_P_CPY_SHP_COD END;
--
--
--   EXCEPTION
--     WHEN OTHERS THEN
--       V_ERR := 'ERR5:'||SQLERRM;
--       O_APP_MSG := 'NEW_P_CPY_SHP_COD='||NEW_P_CPY_SHP_COD||',P_FIG_NO='||P_FIG_NO||','||V_ERR;
--       RETURN;
--     END;

   DELETE
     FROM TSMG_TSFA003
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

   BEGIN
     INSERT INTO TSMG_TSFA003(/*+ parallel(4) */FIG_NO
                            , SHP_COD
                            , PRE_ACT
                            , AFT_ACT
                            , PRE_NWK
                            , AFT_NWK
                            , HO_GUBUN1
                            , HO_GUBUN2
                            , REL_TYP
                            , STD_OST
                            , OFF_SET
                            , FIG_SHP
                            , CPY_SHP
                            , IN_DAT
                            , IN_USR
                            , PRE_ACT_ID
                            , AFT_ACT_ID
                            , REL_STATUS
                            , MANUAL_YN)
      SELECT /*+ parallel(4) */P_FIG_NO FIG_NO
           , P_SHP_COD SHP_COD
           , PRE_ACT
           , AFT_ACT
           , REPLACE (PRE_NWK, P_CPY_FIG_SHP, P_FIG_SHP) PRE_NWK
           , REPLACE (AFT_NWK, P_CPY_FIG_SHP, P_FIG_SHP) AFT_NWK
           , HO_GUBUN1
           , HO_GUBUN2
           , REL_TYP
           , STD_OST
           , OFF_SET
           , P_FIG_SHP FIG_SHP
           , FIG_SHP CPY_SHP
           , TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT
           , P_IN_USR IN_USR
           , PRE_ACT_ID
           , AFT_ACT_ID
           , '' REL_STATUS
           , '' MANUAL_YN
        FROM TSFA003
       WHERE SHP_COD = P_CPY_SHP_COD;
   EXCEPTION
     WHEN OTHERS THEN
       V_ERR := 'ERR6:'||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_1011 : '||V_ERR || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

   -- CASE 의장중일정 Act 관계
   DELETE
     FROM TSMG_TSFN102
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

   BEGIN
     INSERT INTO TSMG_TSFN102(/*+ parallel(4) */FIG_NO
                            , CASE_NO
                            , FIG_SHP
                            , PRE_ACT
                            , AFT_ACT
                            , SHP_COD
                            , CPY_SHP
                            , PRE_NWK
                            , AFT_NWK
                            , HO_GUBUN1
                            , HO_GUBUN2
                            , REL_TYP
                            , STD_OST
                            , OFF_SET
                            , PRE_ACT_ID
                            , AFT_ACT_ID
                            , IP
                            , PID
                            , IN_DAT
                            , IN_USR
                            , PRE_DIV
                            , AFT_DIV
                            , LINE_COLOR
                            , LINE_THICKNESS
                            , LINE_STYLE
                            , LINE_SNAP_LOC)
      SELECT /*+ parallel(4) */P_FIG_NO FIG_NO
           , CASE_NO
           , P_FIG_SHP FIG_SHP
           , PRE_ACT
           , AFT_ACT
           , P_SHP_COD SHP_COD
           , FIG_SHP CPY_SHP
           , REPLACE (PRE_NWK, P_CPY_FIG_SHP, P_FIG_SHP) PRE_NWK
           , REPLACE (AFT_NWK, P_CPY_FIG_SHP, P_FIG_SHP) AFT_NWK
           , HO_GUBUN1
           , HO_GUBUN2
           , REL_TYP
           , STD_OST
           , OFF_SET
           , PRE_ACT_ID
           , AFT_ACT_ID
           , IP
           , PID
           , TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT
           , P_IN_USR IN_USR
           , PRE_DIV
           , AFT_DIV
           , LINE_COLOR
           , LINE_THICKNESS
           , LINE_STYLE
           , LINE_SNAP_LOC
        FROM TSFN102
       WHERE CASE_NO = '000000000000'
         AND SHP_COD = P_CPY_SHP_COD;
   EXCEPTION
     WHEN OTHERS THEN
       V_ERR := 'ERR7:'||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_1078 : '||V_ERR || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

   -- 후행 중일정 호선별 ACT 절점 제약<사업계획용>
   DELETE
     FROM TSMG_TSFN105
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

   BEGIN
     INSERT INTO TSMG_TSFN105(/*+ parallel(4) */FIG_NO
                            , CASE_NO
                            , FIG_SHP
                            , ACT_COD
                            , WRK_PNT
                            , SHP_COD
                            , CPY_SHP
                            , STD_OST
                            , PNT_OST
                            , ST_FI_GBN
                            , RMK
                            , USE_YN
                            , IP
                            , PID
                            , IN_DAT
                            , IN_USR)
      SELECT /*+ parallel(4) */P_FIG_NO FIG_NO
           , CASE_NO
           , P_FIG_SHP FIG_SHP
           , ACT_COD
           , WRK_PNT
           , P_SHP_COD SHP_COD
           , FIG_SHP CPY_SHP
           , STD_OST
           , PNT_OST
           , ST_FI_GBN
           , RMK
           , USE_YN
           , IP
           , PID
           , TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT
           , P_IN_USR IN_USR
        FROM TSFN105
       WHERE CASE_NO = '000000000000'
         AND SHP_COD = P_CPY_SHP_COD;
   EXCEPTION
     WHEN OTHERS THEN
       V_ERR := 'ERR8:'||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_1125 : '||V_ERR || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

  -- 블록의장 절점(KL, FT, LC, DL) 기간별 ACT 착수/완료/계획공기 수정
   BEGIN
  MERGE INTO TSMG_TSFA001 UPDATE_Q
    USING ( WITH SHP_INFO AS
            (
              SELECT MAX(SHP_COD_F) SHP_COD_F, MAX(FIG_NO_F) FIG_NO_F, MAX(FIG_SHP_F) FIG_SHP_F, MAX(KL_F) KL_F, MAX(FT_F) FT_F, MAX(LC_F) LC_F, MAX(DL_F) DL_F
                   , MAX(SHP_COD_T) SHP_COD_T, MAX(FIG_NO_T) FIG_NO_T, MAX(FIG_SHP_T) FIG_SHP_T, MAX(KL_T) KL_T, MAX(FT_T) FT_T, MAX(LC_T) LC_T, MAX(DL_T) DL_T
                   , MAX(KL_FT_F) KL_FT_F, MAX(FT_LC_F) FT_LC_F, MAX(LC_DL_F) LC_DL_F
                   , MAX(KL_FT_T) KL_FT_T, MAX(FT_LC_T) FT_LC_T, MAX(LC_DL_T) LC_DL_T
                FROM (
                      SELECT A.SHP_COD SHP_COD_F, A.FIG_NO FIG_NO_F, A.FIG_SHP FIG_SHP_F, A.KL KL_F, A.FT FT_F, A.LC LC_F, A.DL DL_F
                           , '' SHP_COD_T, '' FIG_NO_T, '' FIG_SHP_T, '' KL_T, '' FT_T, '' LC_T, '' DL_T
                           , FC_GET_NETDAY_TSMG(A.KL, A.FT) KL_FT_F, FC_GET_NETDAY_TSMG(A.FT, A.LC) FT_LC_F, FC_GET_NETDAY_TSMG(A.LC, A.DL) LC_DL_F
                           , 0 KL_FT_T, 0 FT_LC_T, 0 LC_DL_T
                        FROM TSAA002 A
                       WHERE A.SHP_COD = P_CPY_SHP_COD
                       UNION ALL
                      SELECT '' SHP_COD_F, '' FIG_NO_F, '' FIG_SHP_F, '' KL_F, '' FT_F, '' LC_F, '' DL_F
                           , A.SHP_COD SHP_COD_T, A.FIG_NO FIG_NO_T, A.FIG_SHP FIG_SHP_T, A.KL KL_T, A.FT FT_T, A.LC LC_T, A.DL DL_T
                           , 0 KL_FT_F, 0 FT_LC_F, 0 LC_DL_F
                           , FC_GET_NETDAY_TSMG(A.KL, A.FT) KL_FT_T, FC_GET_NETDAY_TSMG(A.FT, A.LC) FT_LC_T, FC_GET_NETDAY_TSMG(A.LC, A.DL) LC_DL_T
                        FROM TSAD001 A
                       WHERE A.FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                         AND A.SHP_COD = P_SHP_COD
                     )
            )
            SELECT A.*
                 , FC_GET_NETDAY_TSMG(CASE WHEN A.PLN_ST_NEW < A.KL_T THEN A.KL_T ELSE A.PLN_ST_NEW END, CASE WHEN A.PLN_FI_NEW < A.KL_T THEN A.KL_T WHEN A.PLN_FI_NEW < A.PLN_ST_NEW THEN A.PLN_ST_NEW ELSE A.PLN_FI_NEW END) PLN_TRM_NEW
                 , CASE WHEN A.PLN_ST_NEW < A.KL_T THEN A.KL_T ELSE A.PLN_ST_NEW END PLN_ST_CHG
                 , CASE WHEN A.PLN_FI_NEW < A.KL_T THEN A.KL_T WHEN A.PLN_FI_NEW < A.PLN_ST_NEW THEN A.PLN_ST_NEW ELSE A.PLN_FI_NEW END PLN_FI_CHG
              FROM (
                    SELECT M.*
                         , CEIL(M.KL_FT_RATE / 2 * M.KL_PLN_ST) KL_PLN_ST_NEW
                         , CEIL(M.KL_FT_RATE / 2 * M.KL_PLN_FI) KL_PLN_FI_NEW, S1.NET_DAY
                         , (SELECT MIN(Z.CAL_DAT)
                              FROM TSAB002 Z
                             WHERE Z.NET_DAY = (S1.NET_DAY + CEIL(M.KL_FT_RATE / 2 * M.KL_PLN_ST) * M.KL_FT_RATE_GB)) PLN_ST_NEW
                         , (SELECT MAX(Z.CAL_DAT)
                              FROM TSAB002 Z
                             WHERE Z.NET_DAY = (S2.NET_DAY - CEIL(M.KL_FT_RATE / 2 * M.KL_PLN_FI) * M.KL_FT_RATE_GB)) PLN_FI_NEW
                      FROM (
                            SELECT A.FIG_NO
                                 , A.SHP_COD
                                 , A.ACT_COD
                                 , A.PLN_ST
                                 , A.PLN_FI
                                 , A.PLN_TRM
                                 , B.KL_T
                                 , B.KL_FT_F
                                 , B.KL_FT_T
                                 , CASE WHEN B.KL_FT_F > B.KL_FT_T THEN ((B.KL_FT_F - B.KL_FT_T) / B.KL_FT_F)
                                        ELSE ((B.KL_FT_T - B.KL_FT_F) / B.KL_FT_F) END KL_FT_RATE
                                 , CASE WHEN B.KL_FT_F > B.KL_FT_T THEN 1
                                        ELSE -1 END KL_FT_RATE_GB
                                 , FC_GET_NETDAY_TSMG(B.KL_T, A.PLN_ST) KL_PLN_ST
                                 , FC_GET_NETDAY_TSMG(B.KL_T, A.PLN_FI) KL_PLN_FI
                                 , FC_GET_NETDAY_TSMG(A.PLN_ST, A.PLN_FI) PLN_ST_FI
                              FROM TSMG_TSFA001 A
                                 , SHP_INFO B
                             WHERE A.FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                               AND A.SHP_COD = P_SHP_COD
                               AND A.PLN_ST IS NOT NULL
                               AND A.PLN_FI IS NOT NULL
                               AND A.PLN_ST BETWEEN B.KL_T AND B.FT_T
                           ) M
                         , TSAB002 S1
                         , TSAB002 S2
                     WHERE 1 = 1
                       AND M.PLN_ST = S1.CAL_DAT
                       AND M.PLN_FI = S2.CAL_DAT
                   ) A
          ) SUB_Q
      ON (UPDATE_Q.FIG_NO = SUB_Q.FIG_NO AND UPDATE_Q.SHP_COD = SUB_Q.SHP_COD AND UPDATE_Q.ACT_COD = SUB_Q.ACT_COD)
     WHEN MATCHED THEN
       UPDATE SET UPDATE_Q.PLN_ST = SUB_Q.PLN_ST_CHG
                , UPDATE_Q.PLN_FI = SUB_Q.PLN_FI_CHG
                , UPDATE_Q.PLN_TRM = SUB_Q.PLN_TRM_NEW;
   EXCEPTION
     WHEN OTHERS THEN
       V_ERR := 'ERR8:'||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_1_1209 : '||V_ERR || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

  -- 구획의장 절점(KL, FT, LC, DL) 기간별 ACT 착수/완료/계획공기 수정
   BEGIN
  MERGE INTO TSMG_TSFN101 UPDATE_Q
    USING ( WITH SHP_INFO AS
            (
              SELECT MAX(SHP_COD_F) SHP_COD_F, MAX(FIG_NO_F) FIG_NO_F, MAX(FIG_SHP_F) FIG_SHP_F, MAX(KL_F) KL_F, MAX(FT_F) FT_F, MAX(LC_F) LC_F, MAX(DL_F) DL_F
                   , MAX(SHP_COD_T) SHP_COD_T, MAX(FIG_NO_T) FIG_NO_T, MAX(FIG_SHP_T) FIG_SHP_T, MAX(KL_T) KL_T, MAX(FT_T) FT_T, MAX(LC_T) LC_T, MAX(DL_T) DL_T
                   , MAX(KL_FT_F) KL_FT_F, MAX(FT_LC_F) FT_LC_F, MAX(LC_DL_F) LC_DL_F
                   , MAX(KL_FT_T) KL_FT_T, MAX(FT_LC_T) FT_LC_T, MAX(LC_DL_T) LC_DL_T
                FROM (
                      SELECT A.SHP_COD SHP_COD_F, A.FIG_NO FIG_NO_F, A.FIG_SHP FIG_SHP_F, A.KL KL_F, A.FT FT_F, A.LC LC_F, A.DL DL_F
                           , '' SHP_COD_T, '' FIG_NO_T, '' FIG_SHP_T, '' KL_T, '' FT_T, '' LC_T, '' DL_T
                           , FC_GET_NETDAY_TSMG(A.KL, A.FT) KL_FT_F, FC_GET_NETDAY_TSMG(A.FT, A.LC) FT_LC_F, FC_GET_NETDAY_TSMG(A.LC, A.DL) LC_DL_F
                           , 0 KL_FT_T, 0 FT_LC_T, 0 LC_DL_T
                        FROM TSAA002 A
                       WHERE A.SHP_COD = P_CPY_SHP_COD
                       UNION ALL
                      SELECT '' SHP_COD_F, '' FIG_NO_F, '' FIG_SHP_F, '' KL_F, '' FT_F, '' LC_F, '' DL_F
                           , A.SHP_COD SHP_COD_T, A.FIG_NO FIG_NO_T, A.FIG_SHP FIG_SHP_T, A.KL KL_T, A.FT FT_T, A.LC LC_T, A.DL DL_T
                           , 0 KL_FT_F, 0 FT_LC_F, 0 LC_DL_F
                           , FC_GET_NETDAY_TSMG(A.KL, A.FT) KL_FT_T, FC_GET_NETDAY_TSMG(A.FT, A.LC) FT_LC_T, FC_GET_NETDAY_TSMG(A.LC, A.DL) LC_DL_T
                        FROM TSAD001 A
                       WHERE A.FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                         AND A.SHP_COD = P_SHP_COD
                     )
            )
            SELECT A.*
                 , FC_GET_NETDAY_TSMG(CASE WHEN A.PLN_ST_NEW < A.KL_T THEN A.KL_T ELSE A.PLN_ST_NEW END, CASE WHEN A.PLN_FI_NEW < A.KL_T THEN A.KL_T WHEN A.PLN_FI_NEW < A.PLN_ST_NEW THEN A.PLN_ST_NEW ELSE A.PLN_FI_NEW END) PLN_TRM_NEW
                 , CASE WHEN A.PLN_ST_NEW < A.KL_T THEN A.KL_T ELSE A.PLN_ST_NEW END PLN_ST_CHG
                 , CASE WHEN A.PLN_FI_NEW < A.KL_T THEN A.KL_T WHEN A.PLN_FI_NEW < A.PLN_ST_NEW THEN A.PLN_ST_NEW ELSE A.PLN_FI_NEW END PLN_FI_CHG
              FROM (
                    SELECT M.*
                         , CEIL(M.KL_FT_RATE / 2 * M.KL_PLN_ST) KL_PLN_ST_NEW
                         , CEIL(M.KL_FT_RATE / 2 * M.KL_PLN_FI) KL_PLN_FI_NEW, S1.NET_DAY
                         , (SELECT MIN(Z.CAL_DAT)
                              FROM TSAB002 Z
                             WHERE Z.NET_DAY = (S1.NET_DAY + CEIL(M.KL_FT_RATE / 2 * M.KL_PLN_ST) * M.KL_FT_RATE_GB)) PLN_ST_NEW
                         , (SELECT MAX(Z.CAL_DAT)
                              FROM TSAB002 Z
                             WHERE Z.NET_DAY = (S2.NET_DAY - CEIL(M.KL_FT_RATE / 2 * M.KL_PLN_FI) * M.KL_FT_RATE_GB)) PLN_FI_NEW
                      FROM (
                            SELECT A.FIG_NO
                                 , A.CASE_NO
                                 , A.SHP_COD
                                 , A.ACT_COD
                                 , A.PLN_ST
                                 , A.PLN_FI
                                 , A.PLN_TRM
                                 , B.KL_T
                                 , B.KL_FT_F
                                 , B.KL_FT_T
                                 , CASE WHEN B.KL_FT_F > B.KL_FT_T THEN ((B.KL_FT_F - B.KL_FT_T) / B.KL_FT_F)
                                        ELSE ((B.KL_FT_T - B.KL_FT_F) / B.KL_FT_F) END KL_FT_RATE
                                 , CASE WHEN B.KL_FT_F > B.KL_FT_T THEN 1
                                        ELSE -1 END KL_FT_RATE_GB
                                 , FC_GET_NETDAY_TSMG(B.KL_T, A.PLN_ST) KL_PLN_ST
                                 , FC_GET_NETDAY_TSMG(B.KL_T, A.PLN_FI) KL_PLN_FI
                                 , FC_GET_NETDAY_TSMG(A.PLN_ST, A.PLN_FI) PLN_ST_FI
                              FROM TSMG_TSFN101 A
                                 , SHP_INFO B
                             WHERE A.FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                               AND A.SHP_COD = P_SHP_COD
                               AND A.CASE_NO = '000000000000'
                               AND A.PLN_ST IS NOT NULL
                               AND A.PLN_FI IS NOT NULL
                               AND A.PLN_ST BETWEEN B.KL_T AND B.FT_T
                           ) M
                         , TSAB002 S1
                         , TSAB002 S2
                     WHERE 1 = 1
                       AND M.PLN_ST = S1.CAL_DAT
                       AND M.PLN_FI = S2.CAL_DAT
                   ) A
          ) SUB_Q
      ON (UPDATE_Q.FIG_NO = SUB_Q.FIG_NO AND UPDATE_Q.CASE_NO = SUB_Q.CASE_NO AND UPDATE_Q.SHP_COD = SUB_Q.SHP_COD AND UPDATE_Q.ACT_COD = SUB_Q.ACT_COD)
     WHEN MATCHED THEN
       UPDATE SET UPDATE_Q.PLN_ST = SUB_Q.PLN_ST_CHG
                , UPDATE_Q.PLN_FI = SUB_Q.PLN_FI_CHG
                , UPDATE_Q.PLN_TRM = SUB_Q.PLN_TRM_NEW;
   EXCEPTION
     WHEN OTHERS THEN
       V_ERR := 'ERR8:'||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_2_1295 : '||V_ERR || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
       RETURN;
     END;

  -- ACT별 OFF_SET 수정
   BEGIN
  MERGE INTO TSMG_TSFN102 UPDATE_Q
    USING ( WITH ACT_LIST AS
            (
              SELECT FIG_NO, SHP_COD, ACT_COD, PLN_ST, PLN_FI
                FROM TSMG_TSFN101
               WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                 AND CASE_NO = '000000000000'
                 AND SHP_COD = P_SHP_COD
                 AND PLN_ST IS NOT NULL

                 AND PLN_FI IS NOT NULL
               UNION
              SELECT FIG_NO, SHP_COD, ACT_COD, PLN_ST, PLN_FI
                FROM TSMG_TSFA001
               WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                 AND SHP_COD = P_SHP_COD
                 AND PLN_ST IS NOT NULL
                 AND PLN_FI IS NOT NULL
            )
            SELECT A.FIG_NO, A.SHP_COD, A.FIG_SHP, A.REL_TYP
                 , A.PRE_ACT, B.PLN_ST PRE_PLN_ST, B.PLN_FI PRE_PLN_FI
                 , A.AFT_ACT, C.PLN_ST AFT_PLN_ST, C.PLN_FI AFT_PLN_FI
                 , CASE WHEN A.REL_TYP = 'SS' THEN FC_GET_NETDAY_TSMG(B.PLN_ST, C.PLN_ST)
                        WHEN A.REL_TYP = 'SF' THEN FC_GET_NETDAY_TSMG(B.PLN_ST, C.PLN_FI) + 1
                        WHEN A.REL_TYP = 'FS' THEN FC_GET_NETDAY_TSMG(B.PLN_FI, C.PLN_ST) - 1
                        WHEN A.REL_TYP = 'FF' THEN FC_GET_NETDAY_TSMG(B.PLN_FI, C.PLN_FI)
                        ELSE 0 END OFF_SET_NEW, A.OFF_SET
              FROM TSMG_TSFN102 A
                 , ACT_LIST B
                 , ACT_LIST C
             WHERE A.FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
               AND A.CASE_NO = '000000000000'
               AND A.SHP_COD = P_SHP_COD
               AND A.FIG_NO = B.FIG_NO
               AND A.SHP_COD = B.SHP_COD
               AND A.PRE_ACT = B.ACT_COD
               AND A.FIG_NO = C.FIG_NO
               AND A.SHP_COD = C.SHP_COD
               AND A.AFT_ACT = C.ACT_COD
          ) SUB_Q
      ON (UPDATE_Q.FIG_NO = SUB_Q.FIG_NO AND UPDATE_Q.CASE_NO = '000000000000' AND UPDATE_Q.FIG_SHP = SUB_Q.FIG_SHP AND UPDATE_Q.PRE_ACT = SUB_Q.PRE_ACT AND UPDATE_Q.AFT_ACT = SUB_Q.AFT_ACT)
     WHEN MATCHED THEN
       UPDATE SET UPDATE_Q.OFF_SET = SUB_Q.OFF_SET_NEW;
   EXCEPTION
     WHEN OTHERS THEN
       V_ERR := 'ERR8:'||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_O_3_1346 : '||V_ERR || ' / ' || $$plsql_unit || ' at ' || $$plsql_line;
     END;

   COMMIT;
END PROC_COPY_PROJECT_DATA1_O;
