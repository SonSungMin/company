CREATE OR REPLACE PROCEDURE PROC_COPY_PROJECT_DATA2_H
(
   P_FIG_NO         TSAD001.FIG_NO%TYPE, -- PPING-NOW
   P_SHP_COD        TSAD001.SHP_COD%TYPE, -- 1046240
   P_FIG_SHP        TSAD001.FIG_SHP%TYPE, -- 2947
   P_CPY_FIG_NO     TSAD001.FIG_NO%TYPE, --999999999
   P_CPY_SHP_COD    TSAD001.SHP_COD%TYPE, -- 1046240
   P_CPY_FIG_SHP    TSAD001.FIG_SHP%TYPE, -- 2947
   P_CPY_DCK_COD    TSAD001.DCK_COD%TYPE, -- 2
   P_IN_USR         TSAD001.IN_USR%TYPE,  -- 입력자
   P_ACT_PLN_YN     VARCHAR2 := 'N', -- Act.일정변경 Y/N
   O_APP_MSG        OUT VARCHAR2
   )
IS
/******************************************************************************
   NAME:       PROC_COPY_PROJECT_DATA2_TSMG_H
   PURPOSE:    모델선에서 사업계획 데이터 생성 (선각)
******************************************************************************/
   V_KL_GAP   NUMBER (8);
   V_ERR      VARCHAR2(4000);
   V_PARAM_DES VARCHAR2(4000);
BEGIN
    V_PARAM_DES := 'P_FIG_NO='||P_FIG_NO||',P_SHP_COD='||P_SHP_COD||',P_FIG_SHP='||P_FIG_SHP||',P_CPY_FIG_NO='||P_CPY_FIG_NO||',P_CPY_SHP_COD='||P_CPY_SHP_COD||',P_CPY_FIG_SHP='||P_CPY_FIG_SHP;

    IF P_ACT_PLN_YN = 'Y' THEN
        SELECT fc_get_netday (A.KL) - fc_get_netday (B.KL)
          INTO V_KL_GAP
          FROM TSAD001 A, TSAD001 B
         WHERE A.FIG_NO = P_FIG_NO
           AND A.FIG_SHP = P_FIG_SHP
           AND B.FIG_NO = P_CPY_FIG_NO
           AND B.FIG_SHP = P_CPY_FIG_SHP;
    ELSE
        BEGIN
            SELECT fc_get_netday (A.KL) - fc_get_netday (B.KL)
              INTO V_KL_GAP
              FROM TSAD001 A, TSAA002 B
             WHERE A.FIG_NO  = P_FIG_NO--RPAD(P_FIG_NO, 9, ' ')
               AND A.FIG_SHP = P_FIG_SHP
               AND B.FIG_SHP = P_CPY_FIG_SHP;
        EXCEPTION WHEN OTHERS THEN
            V_KL_GAP := 0;
        END;
    END IF;
V_ERR := 'STEP1#V_KL_GAP='||V_KL_GAP||',P_FIG_NO='||P_FIG_NO||',P_FIG_SHP='||P_FIG_SHP||',P_CPY_FIG_SHP='||P_CPY_FIG_SHP;

    --운영에서 사업계획
    UPDATE TSAD001
       SET CPY_SHP    = P_CPY_FIG_SHP,
           CPY_FIG_NO = P_CPY_FIG_NO
     WHERE FIG_NO  = P_FIG_NO
       AND SHP_COD = P_SHP_COD;

    DELETE
      FROM TSEG005
     WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
       AND SHP_COD = P_SHP_COD;
    --COMMIT;

   BEGIN
       V_ERR := 'STEP2';
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
                            up_DAT,
                            up_USR,
                            MUL_WGT,
                            MUL_WGT_O,   --2물량
                            MUL_WGT_O_1, --1물량
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
                 --PLN_ST,
                 --PLN_FI,
                 --N.Day 고려 안하고 날짜를 바로 넘기도록 26.02.02 YBK
                 fc_get_calday_pili(PLN_ST) + V_KL_GAP,
                 fc_get_calday_pili(PLN_FI) + V_KL_GAP,
                 --fc_get_calday (fc_get_netday (PLN_ST) + V_KL_GAP),
                 --fc_get_calday (fc_get_netday (PLN_FI) + V_KL_GAP),
                 A.EST_ST,
                 A.EST_FI,
                 STD_TRM,
                 PLN_TRM,
                 NET_TRM,
                 NVL(A.DPT_COD, B.RPLN_STG) AS DPT_COD,
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
                 NVL(CASE WHEN DPT_COD LIKE 'C59%' THEN 0 ELSE CASE WHEN ZZARBEI_PO IS NOT NULL THEN ZZARBEI_PO ELSE MHR_REL END END, 0), -- 실공수   (포항공장부는 0)
                 NVL(CASE WHEN DPT_COD LIKE 'C59%' THEN 0 ELSE CASE WHEN ZZARBEI_PO IS NOT NULL THEN ZZARBEI_PO ELSE MHR_REL END END, 0), -- 표준공수 (포항공장부는 0)
                 EXP_MHR,
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
                 A.up_DAT,
                 A.up_USR,
                 NVL(A.MUL_WGT, B.ZZACT_WT) MUL_WGT,
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
                 A.ZZOUT_CODE,
                 A.MUL_WGT2
            FROM TSEG005 A
               , C51A.T51A0030 B
               , MH_RT Z
               , TSMG033 M
           WHERE FIG_NO = RPAD(P_CPY_FIG_NO, 9, ' ')
             AND SHP_COD = P_CPY_SHP_COD
             AND A.FIG_SHP = Z.FIG_SHP(+)
             AND A.FIG_SHP = B.SHPNO(+)
             AND A.ACT_COD = B.ACT_NO(+)
             AND TRIM(A.ACT_COD) = Z.ACT_COD(+)
             AND SUBSTR(A.POS_ID, 5, 5) = M.WBS_ID(+)
             AND M.OUTFIT_INDC(+) = 'Y'
             AND ( A.ACT_TYP LIKE 'A%' OR A.ACT_TYP LIKE 'Z%' ) --ME0A0N41000  	ME0A0 DOCK 건조 ACT 등. 생성안되어 탑재네트워크에 문제발생 2022.02.03 수정
             AND A.ACT_TYP <> 'A99' --공수, DUMM ACT 생성 시 생성되는 ACT로, 공정과 ACT.생성 후 공수 계산 사이에 발생 한 ACT.를 공수계산용으로 추가하는 것.. 이 것들은 복사 할 필요 없음 2022.11.03
             ;

   EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --COMMIT;
   -- 원단위 적용 UPDATE
--   BEGIN
--   UPDATE TSEG005
--      SET UNIT_GB = CASE WHEN STD_MHR > 0 AND MUL_WGT > 0 THEN 'A'
--                         WHEN STD_MHR > 0 AND MUL_WGT = 0 THEN 'G'
--                         ELSE '' END
--        , UNIT    = CASE WHEN STD_MHR > 0 AND MUL_WGT > 0 THEN MUL_WGT / STD_MHR
--                         ELSE 0 END
--    WHERE FIG_NO = P_FIG_NO
--      AND SHP_COD = P_SHP_COD;
--   EXCEPTION
--     WHEN OTHERS THEN
--       V_ERR := SQLERRM;
--       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H_UNIT_GB : '||V_ERR;
--       RETURN;
--     END;

   -- 원단위(그룹평균)
--   BEGIN
--   UPDATE TSEG005 A
--      SET UNIT    = (SELECT SUM(Z.MUL_WGT) / SUM(Z.STD_MHR)
--                       FROM TSEG005 Z
--                      WHERE Z.FIG_NO = A.FIG_NO
--                        AND Z.SHP_COD = A.SHP_COD
--                        AND Z.POS_ID = A.POS_ID)
--    WHERE A.FIG_NO = P_FIG_NO
--      AND A.SHP_COD = P_SHP_COD
--      AND A.UNIT_GB = 'G';
--   EXCEPTION
--     WHEN OTHERS THEN
--       V_ERR := SQLERRM;
--       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H_UNIT : '||V_ERR;
--       RETURN;
--     END;


   --선각 중일정 ACT 관계
   DELETE
     FROM TSEG006
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;
   --COMMIT;
   BEGIN
       V_ERR := 'STEP3';
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
            FROM TSEG006
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --선각 중일정 ACT 물량
   DELETE
     FROM TSEG007
    WHERE FIG_NO = RPAD(P_FIG_NO, 9, ' ')
      AND SHP_COD = P_SHP_COD;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP4';
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
            FROM TSEG007
           WHERE FIG_NO  = RPAD(P_CPY_FIG_NO, 9, ' ')
             AND SHP_COD = P_CPY_SHP_COD;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --선각블록
   DELETE
     FROM TSEG008
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;
   --COMMIT;
   BEGIN
       V_ERR := 'STEP5';
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
            FROM TSEG008
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --선각블록관계
   DELETE
     FROM TSEG009
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP6';
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
            FROM TSEG009
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --선각 lot
   DELETE
     FROM TSEG010
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP7';
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
            FROM TSEG010
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --공정계열
   DELETE
     FROM TSEG011
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP8';
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
            FROM TSEG011
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;

   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --탑재노드
   DELETE
     FROM TSEG012
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP9';
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
            FROM TSEG012
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --탑재순서
   DELETE
     FROM TSEG013
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP10';
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
            FROM TSEG013
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;

   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   --PE 공법
   DELETE
     FROM TSEG025
    WHERE FIG_NO  = P_FIG_NO
      AND SHP_COD = P_SHP_COD;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP11';
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
            FROM TSEG025
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   -- 외주블록
   DELETE
     FROM TSEG166
    WHERE FIG_NO  = P_FIG_NO
      AND FIG_SHP = P_FIG_SHP;

   --COMMIT;

   BEGIN
       V_ERR := 'STEP12';
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
          SELECT  P_FIG_NO FIG_NO,
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
            FROM TSEG166
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND SHP_COD = P_CPY_SHP_COD;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   -- 호선별 물량 집계(선행의장) 사업계획
   DELETE
     FROM TSEG121
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND FIG_SHP = P_FIG_SHP;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP13';
       INSERT INTO TSEG121 (FIG_NO,
                            FIG_SHP,
                            ACT_COD,
                            LOD_COD,
                            MUL_QTY,
                            MOD_MUL_QTY,
                            MUL_UNT,
                            SHP_COD,
                            MUL_QTY_REAL,
                            MUL_QTY_FORE,
                            LD_COD,
                            ASM_LOD_TOT,
                            MUL_FIX,
                            IN_DAT,
                            IN_USR)
          SELECT P_FIG_NO FIG_NO,
                 P_FIG_SHP FIG_SHP,
                 ACT_COD,
                 LOD_COD,
                 MUL_QTY,
                 MOD_MUL_QTY,
                 MUL_UNT,
                 SHP_COD,
                 MUL_QTY_REAL,
                 MUL_QTY_FORE,
                 LD_COD,
                 ASM_LOD_TOT,
                 MUL_FIX,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR
            FROM TSEG121
           WHERE FIG_NO  = RPAD(P_CPY_FIG_NO, 9, ' ')
             AND FIG_SHP = P_CPY_FIG_SHP;

   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   -- 호선별 구획 정의 사업계획
   DELETE
     FROM TSEG122
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND FIG_SHP = P_FIG_SHP;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP14';
       INSERT INTO TSEG122 (FIG_NO,
                            FIG_SHP,
                            ACT_COD,
                            MEGA_REGION,
                            MID_REGION,
                            REGION_KEY,
                            SOJO_TYPE,
                            SHP_COD,
                            STD_TRM,
                            FINAL_STD_TRM,
                            SCP_COD,
                            SCP_DESC,
                            IN_DAT,
                            IN_USR)
          SELECT P_FIG_NO FIG_NO,
                 P_FIG_SHP FIG_SHP,
                 ACT_COD,
                 MEGA_REGION,
                 MID_REGION,
                 REGION_KEY,
                 SOJO_TYPE,
                 SHP_COD,
                 STD_TRM,
                 FINAL_STD_TRM,
                 SCP_COD,
                 SCP_DESC,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR
            FROM TSEG122
           WHERE FIG_NO  = RPAD(P_CPY_FIG_NO, 9, ' ')
             AND FIG_SHP = P_CPY_FIG_SHP;
   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   -- 사업계획용 호선별 물량 생성 프로세스 관리
   DELETE
     FROM TSEG131
    WHERE FIG_NO  = RPAD(P_FIG_NO, 9, ' ')
      AND FIG_SHP = P_FIG_SHP;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP15';
       INSERT INTO TSEG131 (FIG_NO,
                            FIG_SHP,
                            PRO_CATEGORY,
                            PRO_ID,
                            CONFORM_DATE1,
                            CONFORM_DATE2,
                            CONFORM_USR1,
                            CONFORM_USR2,
                            IN_DAT,
                            IN_USR,
                            REMARKS)
          SELECT P_FIG_NO FIG_NO,
                 P_FIG_SHP FIG_SHP,
                 PRO_CATEGORY,
                 PRO_ID,
                 CONFORM_DATE1,
                 CONFORM_DATE2,
                 CONFORM_USR1,
                 CONFORM_USR2,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR,
                 REMARKS
            FROM TSEG131
           WHERE FIG_NO  = RPAD(P_CPY_FIG_NO, 9, ' ')
             AND FIG_SHP = P_CPY_FIG_SHP;

   --COMMIT;
     EXCEPTION
     WHEN OTHERS THEN
       V_ERR := V_ERR||','||SQLERRM;
       O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES;
       RETURN;
     END;

   DELETE
     FROM TSEG134
    WHERE FIG_NO  = P_FIG_NO
      AND FIG_SHP = P_FIG_SHP;

   --COMMIT;
   BEGIN
       V_ERR := 'STEP16';
       INSERT INTO TSEG134 (FIG_NO,
                            FIG_SHP,
                            ACT_COD,
                            REGION_KEY,
                            LOD_COD,
                            WRK_STG_TYP,
                            WRK_STG,
                            ITM_COD,
                            MUL_WGT,
                            MUL_QTY,
                            MUL_QTY_TYP,
                            SERIES_FIG_SHP,
                            SERIES_MUL_QTY,
                            CREATE_MUL_QTY,
                            CRITERIA_UNIT,
                            MUL_FACTOR,
                            FACTOR_MUL_QTY,
                            ACT_DES,
                            MIS_COD,
                            DCK_COD,
                            DPT_COD,
                            ITM_GRP,
                            SHP_COD,
                            SHP_KND,
                            SHP_TYP_QTY,
                            SHP_DES,
                            SHP_TYP,
                            SHP_TYP_NM,
                            MODEL_FIG_SHP,
                            FINISH_DATE,
                            PLN_ST,
                            PLN_FI,
                            IN_DAT,
                            IN_USR,
                            FI_MUL_QTY)
          SELECT P_FIG_NO FIG_NO,
                 P_FIG_SHP FIG_SHP,
                 ACT_COD,
                 REGION_KEY,
                 LOD_COD,
                 WRK_STG_TYP,
                 WRK_STG,
                 ITM_COD,
                 MUL_WGT,
                 MUL_QTY,
                 MUL_QTY_TYP,
                 SERIES_FIG_SHP,
                 SERIES_MUL_QTY,
                 CREATE_MUL_QTY,
                 CRITERIA_UNIT,
                 MUL_FACTOR,
                 FACTOR_MUL_QTY,
                 ACT_DES,
                 MIS_COD,
                 DCK_COD,
                 DPT_COD,
                 ITM_GRP,
                 P_SHP_COD SHP_COD,
                 SHP_KND,
                 SHP_TYP_QTY,
                 SHP_DES,
                 SHP_TYP,
                 SHP_TYP_NM,
                 MODEL_FIG_SHP,
                 FINISH_DATE,
                 PLN_ST,
                 PLN_FI,
                 TO_CHAR (SYSDATE, 'YYYYMMDDHH24MI') IN_DAT,
                 P_IN_USR IN_USR,
                 FI_MUL_QTY
            FROM TSEG134
           WHERE FIG_NO  = P_CPY_FIG_NO
             AND FIG_SHP = P_CPY_FIG_SHP;

     EXCEPTION WHEN OTHERS THEN
        V_ERR := V_ERR||',PARAM='||V_PARAM_DES||','||SQLERRM;
        O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR;
        RETURN;
    END;
    O_APP_MSG := 'OK';
   COMMIT;
EXCEPTION WHEN OTHERS THEN
    O_APP_MSG := 'PROC_COPY_PROJECT_DATA2_TSMG_H : '||V_ERR||',PARAM='||V_PARAM_DES||','||SQLERRM;
END PROC_COPY_PROJECT_DATA2_H;
