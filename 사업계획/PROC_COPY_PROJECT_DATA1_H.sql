CREATE OR REPLACE PROCEDURE PROC_COPY_PROJECT_DATA1_H
(
    P_FIG_NO         TSAD001.FIG_NO%TYPE, -- PPING-NOW
    P_SHP_COD        TSAD001.SHP_COD%TYPE, -- 1046240
    P_FIG_SHP        TSAD001.FIG_SHP%TYPE, -- 2947
    P_CPY_FIG_NO     TSAD001.FIG_NO%TYPE, --999999999
    P_CPY_SHP_COD    TSAD001.SHP_COD%TYPE, -- 1046240
    P_CPY_FIG_SHP    TSAD001.FIG_SHP%TYPE, -- 2947
    P_CPY_DCK_COD    TSAD001.DCK_COD%TYPE, -- 2
    P_IN_USR         TSAD001.IN_USR%TYPE,  -- 입력자
    P_ACT_PLN_YN     VARCHAR2 := 'N',      -- Act.일정변경 Y/N
    O_APP_MSG        OUT VARCHAR2          -- 오류 메세지
)
IS

/******************************************************************************
   NAME:       PROC_COPY_PROJECT_DATA1_TSMG
   PURPOSE:    운영에서 사업계획(시뮬레이션영역)으로 복사(선각)
******************************************************************************/

   V_KL_GAP   NUMBER (8);
   V_ERR      VARCHAR2(4000);
   V_USE_FLAG   VARCHAR2(10);

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
          FROM TSAD001 A, TSAA002 B
         WHERE A.FIG_NO = P_FIG_NO
           AND A.FIG_SHP = P_FIG_SHP
           AND B.FIG_SHP = P_CPY_FIG_SHP;
    END IF;

     --중일정 중량코드 신설 관련 로직 추가(윤주원 책임,220325)--
    SELECT USE_FLAG
      INTO V_USE_FLAG
      FROM T_HCCODA
     WHERE COM_CODE = 'FIGNO';

   --운영에서 사업계획
   --선각 중일정 ACT
   UPDATE TSAD001 -- 선표기준호선
      SET CPY_SHP = P_CPY_FIG_SHP,
          CPY_FIG_NO = '999999999'
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

    BEGIN

       DELETE
         FROM TSEG005
        WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
          AND SHP_COD = P_SHP_COD;

       IF V_USE_FLAG != 'Y' THEN

            V_ERR := '복사 V_USE_FLAG=N,P_CPY_SHP_COD='||P_CPY_SHP_COD||',P_FIG_NO='||P_FIG_NO||',P_SHP_COD='||P_SHP_COD|| ',삭제 개수=' || SQL%ROWCOUNT;

           INSERT INTO TSEG005 (FIG_NO,
                                SHP_COD,
                                ACT_COD,
                                NWK_ID,
                                ACT_ID,
                                ACT_DES,
                                ACT_TYP,
                                PLN_ST,
                                PLN_FI,
                                EST_ST,
                                EST_FI,
                                STD_TRM,
                                PLN_TRM,
                                NET_TRM,
                                DPT_COD,
                                IO_GBN,
                                VND_COD,
                                MHR_TOT,
                                MHR_STU, -- 실투입공수
                                MHR_SGI, -- 실기성공수
                                MHR_REL, -- 실공수
                                STD_MHR, -- 표준공수
                                EXP_MHR,
                                MHR_LOD,
                                SCV_TYP,
                                INI_STG,
                                ITM_COD,
                                ITM_GRP,
                                MIS_COD,
                                DCK_COD,
                                WRK_TYP,
                                WRK_STG,
                                WRK_TYP2,
                                CPY_SHP,
                                JOO_YN,
                                UNI_STG,
                                JBN_J_X,
                                JBN_J_Y,
                                ROT_ANG1,
                                FIG_SHP,
                                IN_DAT,
                                IN_USR,
                                MUL_WGT,
                                MUL_WGT_O,
                                MUL_WGT_O_1, -- 선각 : 물량1 추가, MUL_WGT에는 기존 선각 중량 데이터 삽입
                                REL_JBN,
                                MHR_GBN,
                                SC_RAT,
                                P_POR_EA,
                                P_PO_IND,
                                P_PO_EA_L,
                                P_PO_EA_I,
                                S_POR_EA,
                                S_PO_IND,
                                S_PO_EA_L,
                                S_PO_EA_I,
                                DP_IND,
                                DP_CODE,
                                FI_DT,
                                EX_DPT,
                                EX_DIV,
                                MMV_STG,
                                HO_GUBUN,
                                POS_ID,
                                PLN_ST_INI,
                                PLN_FI_INI,
                                PLN_TRM_INI,
                                INSHOP_TRM,
                                DPT_COD_OR,
                                STEUS_OR,
                                REGION_KEY,
                                SOJO_TYPE,
                                MOD_STD_TRM,
                                MOD_LOD_COD,
                                MOD_MUL_QTY,
                                MIS_AB_OST,
                                ZZOUT_CODE,
                                STATUS,
                                MUL_WGT2)
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
                     --NVL(B.RPLN_STG, A.DPT_COD) AS DPT_COD, -- TSEA002 의 부서 코드만 가져오게 수정. SSM, 박준 책임 요청 2022-07-11
                     A.DPT_COD, -- 부서
                     IO_GBN,
                     VND_COD,
                     MHR_TOT,
                     --NVL(RT_STU, 0),  -- 실투입공수
                     --H도크도장부인 경우, 선행도장 작업은 실투입=기성으로 처리 2022.07.05
                     CASE WHEN NVL(B.RPLN_STG, A.DPT_COD) = 'C7J000'
                           AND SUBSTR(NVL(A.POS_ID,B.POSID), 5, 5) IN ('FNH32', 'FNHZ1')
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
                     INI_STG,
                     ITM_COD,
                     ITM_GRP,
                     MIS_COD,
                     P_CPY_DCK_COD DCK_COD,
                     WRK_TYP,
                     WRK_STG,
                     WRK_TYP2,
                     P_CPY_FIG_SHP CPY_SHP,
                     JOO_YN,
                     UNI_STG,
                     JBN_J_X,
                     JBN_J_Y,
                     ROT_ANG1,
                     P_FIG_SHP FIG_SHP,
                     TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                     P_IN_USR IN_USR,
                     NVL(A.MUL_WGT, B.ZZACT_WT) AS MUL_WGT,
                     (SELECT NVL(X.MUL_QTY, 0)
                        FROM TSEA001 X
                       WHERE A.SHP_COD = X.SHP_COD(+)
                         AND A.ACT_COD = X.ACT_COD(+)
                         AND M.MUL_UNIT_O = X.LOD_COD(+)) MUL_WGT_O, --2물량
                     (SELECT NVL(X.MUL_QTY, 0)
                        FROM TSEA001 X
                       WHERE A.SHP_COD = X.SHP_COD(+)
                         AND A.ACT_COD = X.ACT_COD(+)
                         AND M.MUL_UNIT = X.LOD_COD(+)) MUL_WGT_O_1, --1물량
                     REL_JBN,
                     MHR_GBN,
                     SC_RAT,
                     P_POR_EA,
                     P_PO_IND,
                     P_PO_EA_L,
                     P_PO_EA_I,
                     S_POR_EA,
                     S_PO_IND,
                     S_PO_EA_L,
                     S_PO_EA_I,
                     DP_IND,
                     DP_CODE,
                     FI_DT,
                     EX_DPT,
                     EX_DIV,
                     MMV_STG,
                     HO_GUBUN,
                     REPLACE (NVL(A.POS_ID,B.POSID), P_CPY_FIG_SHP, P_FIG_SHP) POS_ID,
                     PLN_ST_INI,
                     PLN_FI_INI,
                     PLN_TRM_INI,
                     INSHOP_TRM,
                     DPT_COD_OR,
                     STEUS_OR,
                     REGION_KEY,
                     SOJO_TYPE,
                     MOD_STD_TRM,
                     MOD_LOD_COD,
                     MOD_MUL_QTY,
                     MIS_AB_OST,
                     B.ZZOUT_CODE, --SELECT Z.ZZOUT_CODE FROM C51A.T51A0030 Z WHERE Z.SHPNO = A.FIG_SHP AND Z.ACT_NO = A.ACT_COD)
                     '1',
                     MUL_WGT2
                FROM TSEA002 A
                   , C51A.T51A0030 B
                   , MH_RT Z
                   , TSMG033 M
               WHERE A.SHP_COD = P_CPY_SHP_COD
                 AND A.ACT_COD NOT IN

                     (
                        SELECT ACT_COD
                          FROM TSEG005
                         WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                           AND SHP_COD = P_SHP_COD
                     )
                 AND A.FIG_SHP = Z.FIG_SHP(+)
                 AND A.FIG_SHP = B.SHPNO(+)
                 AND TRIM(A.ACT_COD) = B.ACT_NO(+)
                 AND TRIM(A.ACT_COD) = Z.ACT_COD(+)
                 AND SUBSTR(A.POS_ID, 5, 5) = M.WBS_ID(+)
                 AND M.OUTFIT_INDC(+) = 'Y'
                 AND ( A.ACT_TYP LIKE 'A%' OR A.ACT_TYP LIKE 'Z%' ) --ME0A0N41000  	ME0A0 DOCK 건조 ACT 등. 생성안되어 탑재네트워크에 문제발생 2022.02.03 수정
                 AND A.POS_ID IS NOT NULL -- 2022.02.16
                 ;
        ELSE

            V_ERR := '복사 V_USE_FLAG=Y,P_CPY_SHP_COD='||P_CPY_SHP_COD||',P_FIG_NO='||P_FIG_NO||',P_SHP_COD='||P_SHP_COD|| ',삭제 개수=' || SQL%ROWCOUNT;

            INSERT INTO TSEG005 (FIG_NO,
                            SHP_COD,
                            ACT_COD,
                            NWK_ID,
                            ACT_ID,
                            ACT_DES,
                            ACT_TYP,
                            PLN_ST,
                            PLN_FI,
                            EST_ST,
                            EST_FI,
                            STD_TRM,
                            PLN_TRM,
                            NET_TRM,
                            DPT_COD,
                            IO_GBN,
                            VND_COD,
                            MHR_TOT,
                            MHR_STU, -- 실투입공수
                            MHR_SGI, -- 실기성공수
                            MHR_REL, -- 실공수
                            STD_MHR, -- 표준공수
                            EXP_MHR,
                            MHR_LOD,
                            SCV_TYP,
                            INI_STG,
                            ITM_COD,
                            ITM_GRP,
                            MIS_COD,
                            DCK_COD,
                            WRK_TYP,
                            WRK_STG,
                            WRK_TYP2,
                            CPY_SHP,
                            JOO_YN,
                            UNI_STG,
                            JBN_J_X,
                            JBN_J_Y,
                            ROT_ANG1,
                            FIG_SHP,
                            IN_DAT,
                            IN_USR,
                            MUL_WGT,
                            MUL_WGT_O,
                            MUL_WGT_O_1, -- 선각 : 물량1 추가, MUL_WGT에는 기존 선각 중량 데이터 삽입
                            REL_JBN,
                            MHR_GBN,
                            SC_RAT,
                            P_POR_EA,
                            P_PO_IND,
                            P_PO_EA_L,
                            P_PO_EA_I,
                            S_POR_EA,
                            S_PO_IND,
                            S_PO_EA_L,
                            S_PO_EA_I,
                            DP_IND,
                            DP_CODE,
                            FI_DT,
                            EX_DPT,
                            EX_DIV,
                            MMV_STG,
                            HO_GUBUN,
                            POS_ID,
                            PLN_ST_INI,
                            PLN_FI_INI,
                            PLN_TRM_INI,
                            INSHOP_TRM,
                            DPT_COD_OR,
                            STEUS_OR,
                            REGION_KEY,
                            SOJO_TYPE,
                            MOD_STD_TRM,
                            MOD_LOD_COD,
                            MOD_MUL_QTY,
                            MIS_AB_OST,
                            ZZOUT_CODE,
                            STATUS,
                            MUL_WGT2)
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
                 --NVL(B.RPLN_STG, A.DPT_COD) AS DPT_COD, -- TSEA002 의 부서 코드만 가져오게 수정. SSM, 박준 책임 요청 2022-07-11
                 A.DPT_COD,
                 IO_GBN,
                 VND_COD,
                 MHR_TOT,
                 --NVL(RT_STU, 0),  -- 실투입공수
                 --H도크도장부인 경우, 선행도장 작업은 실투입=기성으로 처리 2022.07.05
                 CASE WHEN NVL(B.RPLN_STG, A.DPT_COD) = 'C7J000'
                       AND SUBSTR(NVL(A.POS_ID,B.POSID), 5, 5) IN ('FNH32', 'FNHZ1')
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
                 INI_STG,
                 ITM_COD,
                 ITM_GRP,
                 MIS_COD,
                 P_CPY_DCK_COD DCK_COD,
                 WRK_TYP,
                 WRK_STG,
                 WRK_TYP2,
                 P_CPY_FIG_SHP CPY_SHP,
                 JOO_YN,
                 UNI_STG,
                 JBN_J_X,
                 JBN_J_Y,
                 ROT_ANG1,
                 P_FIG_SHP FIG_SHP,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR,
                 NVL(A.MUL_WGT2, B.ZZACT_WT) AS MUL_WGT,
                 (SELECT NVL(X.MUL_QTY, 0)
                    FROM TSEA001 X
                   WHERE A.SHP_COD = X.SHP_COD(+)
                     AND A.ACT_COD = X.ACT_COD(+)
                     AND M.MUL_UNIT_O = X.LOD_COD(+)) MUL_WGT_O, --2물량
                 (SELECT NVL(X.MUL_QTY, 0)
                    FROM TSEA001 X
                   WHERE A.SHP_COD = X.SHP_COD(+)
                     AND A.ACT_COD = X.ACT_COD(+)
                     AND M.MUL_UNIT = X.LOD_COD(+)) MUL_WGT_O_1, --1물량
                 REL_JBN,
                 MHR_GBN,
                 SC_RAT,
                 P_POR_EA,
                 P_PO_IND,
                 P_PO_EA_L,
                 P_PO_EA_I,
                 S_POR_EA,
                 S_PO_IND,
                 S_PO_EA_L,
                 S_PO_EA_I,
                 DP_IND,
                 DP_CODE,
                 FI_DT,
                 EX_DPT,
                 EX_DIV,
                 MMV_STG,
                 HO_GUBUN,
                 REPLACE (NVL(A.POS_ID,B.POSID), P_CPY_FIG_SHP, P_FIG_SHP) POS_ID,
                 PLN_ST_INI,
                 PLN_FI_INI,
                 PLN_TRM_INI,
                 INSHOP_TRM,
                 DPT_COD_OR,
                 STEUS_OR,
                 REGION_KEY,
                 SOJO_TYPE,
                 MOD_STD_TRM,
                 MOD_LOD_COD,
                 MOD_MUL_QTY,
                 MIS_AB_OST,
                 B.ZZOUT_CODE, --SELECT Z.ZZOUT_CODE FROM C51A.T51A0030 Z WHERE Z.SHPNO = A.FIG_SHP AND Z.ACT_NO = A.ACT_COD)
                 '2',
                 MUL_WGT2
            FROM TSEA002 A
               , C51A.T51A0030 B
               , MH_RT Z
               , TSMG033 M
           WHERE A.SHP_COD = P_CPY_SHP_COD
             AND A.ACT_COD NOT IN
                 (
                    SELECT ACT_COD
                      FROM TSEG005
                     WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
                       AND SHP_COD = P_SHP_COD
                 )
             AND A.FIG_SHP = Z.FIG_SHP(+)
             AND A.FIG_SHP = B.SHPNO(+)
             AND TRIM(A.ACT_COD) = B.ACT_NO(+)
             AND TRIM(A.ACT_COD) = Z.ACT_COD(+)
             AND SUBSTR(A.POS_ID, 5, 5) = M.WBS_ID(+)
             AND M.OUTFIT_INDC(+) = 'Y'
             AND ( A.ACT_TYP LIKE 'A%' OR A.ACT_TYP LIKE 'Z%' ) --ME0A0N41000  	ME0A0 DOCK 건조 ACT 등. 생성안되어 탑재네트워크에 문제발생 2022.02.03 수정
             AND A.POS_ID IS NOT NULL -- 2022.02.16
             ;

        END IF;

        V_ERR := '누락 데이터 복사 P_FIG_NO='||P_FIG_NO||',P_SHP_COD='||P_SHP_COD||',P_CPY_SHP_COD='||P_CPY_SHP_COD||',P_CPY_FIG_SHP='||P_CPY_FIG_SHP||',이전 추가 ACT.갯수='||SQL%ROWCOUNT;

        -- TSEA002 에서 누락된 데이터 추가
       INSERT INTO TSEG005 (FIG_NO,
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
                            MUL_WGT,
                            MUL_WGT_O,
                            MUL_WGT_O_1,
                            PLN_ST_INI,
                            PLN_FI_INI,
                            ZZOUT_CODE,
                            POS_ID,
                            ACT_TYP,
                            STATUS)
          WITH MH_RT AS
          (
            SELECT Z.FIG_SHP, Z.ACT_COD
                 , NVL((SUM(ARBEI_ACTCOST) + SUM(ARBEI_ACTCOSTE) + SUM(ARBEI_SPTEDS) + SUM(ARBEI_SPTEDO)), 0) RT_SGI -- 실투입 = 직영실투입 + 협력사실투입, ※실공수 = 표준 공수
                 , NVL((SUM(ARBEI_BCWP)    + SUM(ARBEI_BCWPE)), 0) RT_STU -- 기성 = 직영기성 + 협력사기성
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
                 NVL(RT_SGI, 0),  -- 실투입공수
                 NVL(RT_STU, 0),  -- 실기성공수
                 CASE WHEN (RPLN_STG LIKE 'C59%' OR RPLN_STG LIKE 'X1%') THEN 0 ELSE NVL(A.ZZARBEI_PO,0) END, -- 실공수
                 CASE WHEN (RPLN_STG LIKE 'C59%' OR RPLN_STG LIKE 'X1%') THEN 0 ELSE NVL(A.ZZARBEI_PO,0) END, -- 표준공수
                 P_CPY_DCK_COD DCK_COD,
                 A.PJTKND WRK_TYP,
                 A.PRO WRK_STG,
                 A.UNTPJT WRK_TYP2,
                 P_CPY_FIG_SHP CPY_SHP,
                 P_FIG_SHP FIG_SHP,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR,
                 A.ZZACT_WT MUL_WGT,
                 (SELECT NVL(X.MUL_QTY, 0)
                    FROM TSEA001 X
                   WHERE A.SHPNO = X.FIG_SHP(+)
                     AND RPAD(A.ACT_NO, 13, ' ') = X.ACT_COD(+)
                     AND M.MUL_UNIT_O = X.LOD_COD(+)) MUL_WGT_O, --2물량
                 (SELECT NVL(X.MUL_QTY, 0)
                    FROM TSEA001 X
                   WHERE A.SHPNO = X.FIG_SHP(+)
                     AND RPAD(A.ACT_NO, 13, ' ') = X.ACT_COD(+)
                     AND M.MUL_UNIT = X.LOD_COD(+)) MUL_WGT_O_1, --1물량
                 fc_get_calday (fc_get_netday (RPLN_SDTE) + V_KL_GAP),
                 fc_get_calday (fc_get_netday (RPLN_FDTE) + V_KL_GAP),
                 --(SELECT Z.ZZOUT_CODE FROM C51A.T51A0030 Z WHERE Z.SHPNO = A.SHPNO AND Z.ACT_NO = A.ACT_NO)
                 A.ZZOUT_CODE,
                 REPLACE (A.POSID, P_CPY_FIG_SHP, P_FIG_SHP) POS_ID,
                 A.ZZACT_ATTRIB,
                 '3'
            FROM (
                    SELECT *
                      FROM C51A.T51A0030
                     WHERE PRO||PJTKND IN
                           (
                            SELECT WRK_STG||WRK_TYP
                              FROM TSEE002
                             WHERE HO_GUBUN IN ('1','3','4')
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
             AND TRIM(A.ACT_NO) = Z.ACT_COD(+)
--             AND NOT EXISTS (SELECT X.ACT_COD
--                               FROM TSEG005 X
--                              WHERE X.FIG_NO  = P_FIG_NO
--                                AND X.SHP_COD = P_SHP_COD --P_CPY_SHP_COD
--                                AND X.ACT_COD = RPAD(A.ACT_NO, 13, ' '))
             AND A.ACT_NO NOT IN (
                                    -- 이미 추가된 ACT.는 제외
                                    SELECT TRIM(ACT_COD)
                                      FROM TSEG005
                                     WHERE FIG_NO = RPAD(P_FIG_NO, 9, ' ')
                                       AND SHP_COD = P_SHP_COD
                                 )
             AND SUBSTR(A.POSID, 5, 5) = M.WBS_ID(+);
--             AND M.OUTFIT_INDC(+) = 'Y';

        V_ERR := '복사완료 P_FIG_NO='||P_FIG_NO||',P_SHP_COD='||P_SHP_COD||', CNT2='||SQL%ROWCOUNT;

    EXCEPTION WHEN OTHERS THEN
        V_ERR := V_ERR || ', 현재 추가ACT.갯수='|| SQL%ROWCOUNT||', ' ||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

    -- 원단위 적용 UPDATE
--    BEGIN
--
--       UPDATE TSEG005
--          SET UNIT_GB = CASE WHEN STD_MHR > 0 AND MUL_WGT > 0 THEN 'A'
--                             WHEN STD_MHR > 0 AND MUL_WGT = 0 THEN 'G'
--                             ELSE '' END
--            , UNIT    = CASE WHEN STD_MHR > 0 AND MUL_WGT > 0 THEN MUL_WGT / STD_MHR
--                             ELSE 0 END
--        WHERE FIG_NO = P_FIG_NO
--          AND SHP_COD = P_SHP_COD;
--
--    EXCEPTION WHEN OTHERS THEN
--        V_ERR := SQLERRM;
--        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H_UNIT_GB : '||V_ERR;
--        RETURN;
--    END;

    -- 원단위(그룹평균)
--    BEGIN
--
--        UPDATE TSEG005 A
--           SET UNIT    = (SELECT SUM(Z.MUL_WGT) / SUM(Z.STD_MHR)
--                            FROM TSEG005 Z
--                           WHERE Z.FIG_NO = A.FIG_NO
--                             AND Z.SHP_COD = A.SHP_COD
--                             AND Z.POS_ID = A.POS_ID)
--         WHERE A.FIG_NO = P_FIG_NO
--           AND A.SHP_COD = P_SHP_COD
--           AND A.UNIT_GB = 'G';
--
--    EXCEPTION WHEN OTHERS THEN
--        V_ERR := SQLERRM;
--        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H_UNIT : '||V_ERR;
--        RETURN;
--    END;



   --선각 중일정 ACT 관계
    DELETE
      FROM TSEG006
     WHERE FIG_NO  = P_FIG_NO
       AND SHP_COD = P_SHP_COD;

    BEGIN
        INSERT INTO TSEG006 (FIG_NO,
                            SHP_COD,
                            PRE_ACT,
                            AFT_ACT,
                            AFT_NWK,
                            PRE_NWK,
                            REL_TYP,
                            STD_OST,
                            OFF_SET,
                            FIG_SHP,
                            IN_DAT,
                            IN_USR)
        SELECT P_FIG_NO FIG_NO,
               P_SHP_COD SHP_COD,
               PRE_ACT,
               AFT_ACT,
               REPLACE (AFT_NWK, P_CPY_FIG_SHP, P_FIG_SHP) AFT_NWK,
               REPLACE (PRE_NWK, P_CPY_FIG_SHP, P_FIG_SHP) PRE_NWK,
               REL_TYP,
               STD_OST,
               OFF_SET,
               P_FIG_SHP FIG_SHP,
               TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
               P_IN_USR IN_USR
          FROM TSEA007 A
         WHERE SHP_COD = P_CPY_SHP_COD
           AND NOT EXISTS
               (
                SELECT SHP_COD
                  FROM TSEG006 X
                 WHERE X.FIG_NO  = P_FIG_NO
                   AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                   AND X.PRE_ACT = A.PRE_ACT
                   AND X.AFT_ACT = A.AFT_ACT
               );

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '선각 중일정 ACT 관계 복사 P_FIG_NO='||P_FIG_NO||',P_SHP_COD='||P_SHP_COD||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

   --선각 중일정 ACT 물량 #################################
   DELETE
     FROM TSEG007
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

    BEGIN
        INSERT INTO TSEG007 (FIG_NO,
                            SHP_COD,
                            ACT_COD,
                            LOD_COD,
                            MUL_QTY,
                            MUL_UNT,
                            MUL_QTY_REAL,
                            MUL_QTY_FORE,
                            FIG_SHP,
                            IN_DAT,
                            IN_USR,
                            LD_COD,
                            ASM_LOD_TOT)
        SELECT P_FIG_NO FIG_NO,
               P_SHP_COD SHP_COD,
               ACT_COD,
               LOD_COD,
               MUL_QTY,
               MUL_UNT,
               MUL_QTY_REAL,
               MUL_QTY_FORE,
               P_FIG_SHP FIG_SHP,
               TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
               P_IN_USR IN_USR,
               LD_COD,
               ASM_LOD_TOT
          FROM TSEA001 A
         WHERE SHP_COD = P_CPY_SHP_COD
           AND NOT EXISTS
               (
                SELECT SHP_COD
                  FROM TSEG007 X
                 WHERE X.FIG_NO  = P_FIG_NO
                   AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                   AND X.ACT_COD = A.ACT_COD
                   AND X.LOD_COD = A.LOD_COD
               );

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '선각 중일정 ACT 물량 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

    -- 생성된 물량을 기준으로 Activity의


   --선각블록
   DELETE
     FROM TSEG008
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

    BEGIN
        INSERT INTO TSEG008 (FIG_NO,
                        SHP_COD,
                        ITM_COD,
                        BLK_TYP,
                        BLK_HIS,
                        MIS_YN,
                        MIS_COD,
                        MIS_IND,
                        MIS_BLK,
                        BLK_WGT,
                        WGT_TOT,
                        BLK_LEN,
                        BLK_WID,
                        BLK_HEI,
                        MRK_STG,
                        ASM_BAS,
                        OUT_BAS,
                        PP_COD,
                        BLK_SHA,
                        BLK_LST,
                        SIZ_C,
                        SIZ_D,
                        SIZ_E,
                        IO_GBN,
                        OUT_BUM,
                        VND_COD,
                        BLK_INS,
                        SEPCIAL_BLK,
                        FIG_SHP,
                        IN_DAT,
                        IN_USR,
                        WED_OUT_PRI,
                        BLK_NET_WGT,
                        PE_AREA,
                        BLK_LEN_BD,
                        BLK_WID_BD,
                        BLK_HEI_BD)
        SELECT P_FIG_NO FIG_NO,
             P_SHP_COD SHP_COD,
             ITM_COD,
             BLK_TYP,
             BLK_HIS,
             MIS_YN,
             MIS_COD,
             MIS_IND,
             MIS_BLK,
             BLK_WGT,
             WGT_TOT,
             BLK_LEN,
             BLK_WID,
             BLK_HEI,
             MRK_STG,
             ASM_BAS,
             OUT_BAS,
             PP_COD,
             BLK_SHA,
             BLK_LST,
             SIZ_C,
             SIZ_D,
             SIZ_E,
             IO_GBN,
             OUT_BUM,
             VND_COD,
             BLK_INS,
             SEPCIAL_BLK,
             P_FIG_SHP FIG_SHP,
             TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
             P_IN_USR IN_USR,
             WED_OUT_PRI,
             BLK_NET_WGT,
             PE_AREA,
             BLK_LEN_BD,
             BLK_WID_BD,
             BLK_HEI_BD
        FROM TSED002 A
       WHERE SHP_COD = P_CPY_SHP_COD
         AND NOT EXISTS
             (
              SELECT SHP_COD
                FROM TSEG008 X
               WHERE X.FIG_NO  = P_FIG_NO
                 AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                 AND X.ITM_COD = A.ITM_COD
             )
       ;

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '선각 블록 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

    --선각블록관계
    DELETE
      FROM TSEG009
     WHERE FIG_NO  = P_FIG_NO
       AND SHP_COD = P_SHP_COD;

    BEGIN
        INSERT INTO TSEG009 (FIG_NO,
                        SHP_COD,
                        ITM_COD,
                        UPP_BLK,
                        LOW_BLK,
                        RIG_BLK,
                        LEF_BLK,
                        MO_BLK,
                        FIG_SHP,
                        IN_DAT,
                        IN_USR)
        SELECT P_FIG_NO FIG_NO,
             P_SHP_COD SHP_COD,
             ITM_COD,
             UPP_BLK,
             LOW_BLK,
             RIG_BLK,
             LEF_BLK,
             MO_BLK,
             P_FIG_SHP FIG_SHP,
             TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
             P_IN_USR IN_USR
        FROM TSED003 A
       WHERE SHP_COD = P_CPY_SHP_COD
         AND NOT EXISTS
             (
              SELECT SHP_COD
                FROM TSEG009 X
               WHERE X.FIG_NO  = P_FIG_NO
                 AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                 AND X.ITM_COD = A.ITM_COD
             )
       ;

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '선각 블록 관계 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

   --선각 lot
   DELETE
     FROM TSEG010
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

    BEGIN
        INSERT INTO TSEG010 (FIG_NO,
                        SHP_COD,
                        ITM_COD,
                        REP_BLK,
                        LOT_WGT,
                        BLK_LST,
                        ADD_LOT_YN,
                        LOT_NET_WGT,
                        REP_BLK_LIST,
                        ADD_REP_BLK,
                        IN_DAT,
                        IN_USR,
                        FIG_SHP)
        SELECT P_FIG_NO FIG_NO,
             P_SHP_COD SHP_COD,
             ITM_COD,
             REP_BLK,
             LOT_WGT,
             BLK_LST,
             ADD_LOT_YN,
             LOT_NET_WGT,
             REP_BLK_LIST,
             ADD_REP_BLK,
             TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
             P_IN_USR IN_USR,
             P_FIG_SHP FIG_SHP
        FROM TSED001 A
       WHERE SHP_COD = P_CPY_SHP_COD
         AND NOT EXISTS
             (
              SELECT SHP_COD
                FROM TSEG010 X
               WHERE X.FIG_NO  = P_FIG_NO
                 AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                 AND X.ITM_COD = A.ITM_COD
                 AND X.REP_BLK = A.REP_BLK
             )
       ;

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '선각 LOT 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

   --공정계열
   DELETE
     FROM TSEG011
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

    BEGIN
        INSERT INTO TSEG011 (FIG_NO,
                        SHP_COD,
                        ASM_BLK,
                        UNT_BLK,
                        ASM_YD,
                        UNT_YD,
                        OUT_ROUTING,
                        IN_ROUTING,
                        FIG_SHP,
                        IN_DAT,
                        IN_USR)
        SELECT P_FIG_NO FIG_NO,
             P_SHP_COD SHP_COD,
             ASM_BLK,
             UNT_BLK,
             ASM_YD,
             UNT_YD,
             OUT_ROUTING,
             IN_ROUTING,
             P_FIG_SHP FIG_SHP,
             TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
             P_IN_USR IN_USR
        FROM TSEE008 A
       WHERE SHP_COD = P_CPY_SHP_COD
         AND NOT EXISTS
             (
              SELECT SHP_COD
                FROM TSEG011 X
               WHERE X.FIG_NO  = P_FIG_NO
                 AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                 AND X.ASM_BLK = A.ASM_BLK
                 AND X.UNT_BLK = A.UNT_BLK
             )
       ;

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '공정계열 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

   --탑재노드
   DELETE
     FROM TSEG012
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

    BEGIN
        INSERT INTO TSEG012 (FIG_NO,
                        SHP_COD,
                        MIS_NOD,
                        BLK_LST,
                        MAI_EVE,

                        OFF_SET,
                        ENT_NET,
                        LNT_NET,
                        OPT_NET,
                        NET_MIS_DAT,
                        NOD_X,
                        NOD_Y,
                        NOD_WID,
                        NOD_HIG,
                        WGT_TOT,
                        DCK_COD,
                        DAE_NOD,
                        DAE_LST,
                        PAR_YN,
                        GCS_TM,
                        SCH_MHD,
                        HO_GUBUN,
                        NOD_COL,
                        KL_YN,
                        WRK_PNT,
                        FIG_SHP,
                        IN_DAT,
                        IN_USR,
                        FT_FA,
                        OUT_FLG,
                        ABS_ENT,
                        ABS_LNT,
                        ABS_OPT)
        SELECT P_FIG_NO FIG_NO,
             P_SHP_COD SHP_COD,
             MIS_NOD,
             BLK_LST,
             MAI_EVE,
             OFF_SET,
             ENT_NET,
             LNT_NET,
             OPT_NET,
             NET_MIS_DAT,
             NOD_X,
             NOD_Y,
             NOD_WID,
             NOD_HIG,
             WGT_TOT,
             P_CPY_DCK_COD DCK_COD,
             DAE_NOD,
             DAE_LST,
             PAR_YN,
             GCS_TM,
             SCH_MHD,
             HO_GUBUN,
             NOD_COL,
             KL_YN,
             WRK_PNT,
             P_FIG_SHP FIG_SHP,
             TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
             P_IN_USR IN_USR,
             FT_FA,
             OUT_FLG,
             ABS_ENT,
             ABS_LNT,
             ABS_OPT
        FROM TSEB001 A
       WHERE SHP_COD = P_CPY_SHP_COD
         AND NOT EXISTS
             (
              SELECT SHP_COD
                FROM TSEG012 X
               WHERE X.FIG_NO  = P_FIG_NO
                 AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                 AND X.MIS_NOD = A.MIS_NOD
             )
       ;

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '탑재 노드 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

   --탑재순서
   DELETE
     FROM TSEG013
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

    BEGIN
        INSERT INTO TSEG013 (FIG_NO,
                        SHP_COD,
                        PRE_NOD,
                        AFT_NOD,
                        ABS_TRM,
                        STD_PCH,
                        PLN_PCH,
                        MIS_JOE,
                        LINE_TYPE,
                        LINE_COL,
                        CLINE_XY,
                        FIG_SHP,
                        IN_DAT,
                        IN_USR,
                        END_LINE_YN)
        SELECT P_FIG_NO FIG_NO,
             P_SHP_COD SHP_COD,
             PRE_NOD,
             AFT_NOD,
             ABS_TRM,
             STD_PCH,
             PLN_PCH,
             MIS_JOE,
             LINE_TYPE,
             LINE_COL,
             CLINE_XY,
             P_FIG_SHP FIG_SHP,
             TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
             P_IN_USR IN_USR,
             END_LINE_YN
        FROM TSEB003 A
       WHERE SHP_COD = P_CPY_SHP_COD
         AND NOT EXISTS
             (
              SELECT SHP_COD
                FROM TSEG013 X
               WHERE X.FIG_NO  = P_FIG_NO
                 AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                 AND X.PRE_NOD = A.PRE_NOD
                 AND X.AFT_NOD = A.AFT_NOD
             )
       ;

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '탑재 순서 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

   --PE 공법
   DELETE
     FROM TSEG025
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

    BEGIN

        INSERT INTO TSEG025 (FIG_NO,
                        SHP_COD,
                        ITM_COD,
                        LOW_BLK,
                        LAYER,
                        SEQ_NO,
                        DECK,
                        BLK_PE_OST_PS,
                        BLK_PE_OST_PP,
                        NET_ST,
                        NET_FI,
                        PLN_TRM,
                        MUL_UNT,
                        CHA_IND,
                        MIS_YN,
                        FIG_SHP,
                        IN_DAT,
                        IN_USR,
                        PE_AREA)
          SELECT P_FIG_NO FIG_NO,
                 P_SHP_COD SHP_COD,
                 ITM_COD,
                 LOW_BLK,
                 LAYER,
                 SEQ_NO,
                 DECK,
                 BLK_PE_OST_PS,
                 BLK_PE_OST_PP,
                 NET_ST,
                 NET_FI,
                 PLN_TRM,
                 MUL_UNT,
                 CHA_IND,
                 MIS_YN,
                 P_FIG_SHP FIG_SHP,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR,
                 PE_AREA
            FROM TSED007 A
           WHERE SHP_COD = P_CPY_SHP_COD
             AND NOT EXISTS
                 (
                  SELECT SHP_COD
                    FROM TSEG025 X
                   WHERE X.FIG_NO  = P_FIG_NO
                     AND X.SHP_COD = P_SHP_COD--A.SHP_COD
                     AND X.ITM_COD = A.ITM_COD
                     AND X.LOW_BLK = A.LOW_BLK
                     AND X.LAYER   = A.LAYER
                 )
           ;

    EXCEPTION WHEN OTHERS THEN
        V_ERR := 'PE 공법 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_TSMG_H : '||V_ERR;
        RETURN;
    END;

    -- 호선별 구획 정의
/*    DELETE
      FROM TSEG122
     WHERE FIG_NO = P_FIG_NO
       AND FIG_SHP = P_FIG_SHP;
       
    BEGIN 
       INSERT INTO TSEG122 (*/
       

    -- 외주 블록
    DELETE
      FROM TSEG166
     WHERE FIG_NO  = P_FIG_NO
       AND FIG_SHP = P_FIG_SHP;

    BEGIN
        INSERT INTO TSEG166 (FIG_NO,
                        FIG_SHP,
                        SHP_COD,
                        ACT_COD,
                        ITM_COD,
                        ITM_GRP,
                        MIS_COD,
                        WRK_STG,
                        WRK_TYP,
                        MUL_WGT,
                        SCP_ST,
                        SCP_FI,
                        DUE_DATE,
                        REGION_KEY,
                        MEGA_REGION_KEY,
                        MEGA_REGION,
                        MID_REGION_KEY,
                        MID_REGION,
                        DPT_COD,
                        DPT_DESC,
                        VND_COD,
                        VND_DESC,
                        SCP_COD,
                        SCP_DESC,
                        SHP_KND,
                        DCK_COD,
                        OWNRP_NM,
                        SHP_TYP_QTY,
                        SHP_TYP_NM,
                        KL,
                        DL,
                        BLK_LEN,
                        BLK_WID,
                        IN_DAT,
                        IN_USR)
          SELECT P_FIG_NO FIG_NO,
                 P_FIG_SHP FIG_SHP,
                 P_SHP_COD SHP_COD,
                 ACT_COD,
                 ITM_COD,
                 ITM_GRP,
                 MIS_COD,
                 WRK_STG,
                 WRK_TYP,
                 MUL_WGT,
                 SCP_ST,
                 SCP_FI,
                 DUE_DATE,
                 REGION_KEY,
                 MEGA_REGION_KEY,
                 MEGA_REGION,
                 MID_REGION_KEY,
                 MID_REGION,
                 DPT_COD,
                 DPT_DESC,
                 VND_COD,
                 VND_DESC,
                 SCP_COD,
                 SCP_DESC,
                 SHP_KND,
                 DCK_COD,
                 OWNRP_NM,
                 SHP_TYP_QTY,
                 SHP_TYP_NM,
                 KL,
                 DL,
                 BLK_LEN,
                 BLK_WID,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR
            FROM TSEO166 A
           WHERE FIG_SHP = P_CPY_FIG_SHP
             AND NOT EXISTS
                 (
                  SELECT SHP_COD
                    FROM TSEG166 X
                   WHERE X.FIG_NO  = P_FIG_NO
                     AND X.SHP_COD = P_SHP_COD
                     AND X.FIG_SHP = P_FIG_SHP
                     AND X.ACT_COD = A.ACT_COD
                 )
           ;

        O_APP_MSG := 'OK';

    EXCEPTION WHEN OTHERS THEN
        V_ERR := '외주 블록 복사 P_FIG_NO='||P_FIG_NO||',P_CPY_FIG_SHP='||P_CPY_FIG_SHP||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_H : '||V_ERR;
        RETURN;
    END;

    PROC_COPY_PROJECT_MUL1(P_FIG_NO, P_SHP_COD, P_FIG_SHP, P_CPY_SHP_COD, P_CPY_FIG_SHP,P_IN_USR,O_APP_MSG);
    COMMIT;

EXCEPTION WHEN OTHERS THEN
    O_APP_MSG := 'PROC_COPY_PROJECT_DATA1_H : '||SQLERRM||',P_CPY_FIG_SHP='||P_CPY_FIG_SHP || ' ( Error raised in: '|| $$plsql_unit ||' at line ' || $$plsql_line || ')';

END PROC_COPY_PROJECT_DATA1_H;
