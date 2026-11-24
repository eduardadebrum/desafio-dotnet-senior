SET DEFINE OFF SERVEROUTPUT ON
WHENEVER SQLERROR EXIT FAILURE

DECLARE
    TYPE t_prestadores IS TABLE OF GUIAS.PRESTADOR_ID%TYPE;
    TYPE t_competencias IS TABLE OF GUIAS.COMPETENCIA%TYPE;
    v_prestadores t_prestadores;
    v_competencias t_competencias;
BEGIN
    SELECT g.PRESTADOR_ID, g.COMPETENCIA
      BULK COLLECT INTO v_prestadores, v_competencias
      FROM GUIAS g
     WHERE g.SITUACAO = 'A'
       AND g.VALOR_ISS <> (SELECT NVL(SUM(ROUND((n.VALOR_SERVICOS - n.VALOR_DEDUCOES) * n.ALIQUOTA, 2)), 0)
                             FROM NFSE n
                            WHERE n.PRESTADOR_ID = g.PRESTADOR_ID
                              AND n.COMPETENCIA = g.COMPETENCIA
                              AND n.SITUACAO = 'N'
                              AND n.ISS_RETIDO = 'N');

    FOR i IN 1 .. v_prestadores.COUNT LOOP
        PKG_GUIA.recalcular_guia(v_prestadores(i), v_competencias(i));
        COMMIT;
    END LOOP;

    DBMS_OUTPUT.PUT_LINE('guias abertas recalculadas: ' || v_prestadores.COUNT);
END;
/

EXIT
