-- Auditoria no nível do banco: qualquer alteração em notas_fiscais (inclusive fora da
-- aplicação, ex.: um UPDATE manual) gera um evento 'BANCO_TRIGGER' na timeline.
CREATE OR REPLACE FUNCTION t_auditar_notas_fiscais()
RETURNS TRIGGER AS $$
DECLARE
    v_alteracoes JSONB := '{}'::JSONB;
BEGIN
    IF (TG_OP = 'INSERT') THEN
        INSERT INTO timeline_eventos_fiscais (
            documento_id, origem_evento, etapa, status_anterior, status_atual,
            alteracoes_campos, detalhe_erro, sucesso
        ) VALUES (
            NEW.id, 'BANCO_TRIGGER', 'CRIACAO_DOCUMENTO', NULL, NEW.status_validacao,
            to_jsonb(NEW) - 'xml_original' - 'xml_assinado', -- omite XML grande para manter o log leve
            NEW.mensagem_erro,
            (NEW.status_validacao <> 'ERRO')
        );
        RETURN NEW;

    ELSIF (TG_OP = 'UPDATE') THEN
        -- Registra apenas os campos relevantes que realmente mudaram: {"campo": [antes, depois]}
        IF (OLD.status_validacao IS DISTINCT FROM NEW.status_validacao) THEN
            v_alteracoes := v_alteracoes || jsonb_build_object('status_validacao', jsonb_build_array(OLD.status_validacao, NEW.status_validacao));
        END IF;
        IF (OLD.mensagem_erro IS DISTINCT FROM NEW.mensagem_erro) THEN
            v_alteracoes := v_alteracoes || jsonb_build_object('mensagem_erro', jsonb_build_array(OLD.mensagem_erro, NEW.mensagem_erro));
        END IF;
        IF (OLD.modelo IS DISTINCT FROM NEW.modelo) THEN
            v_alteracoes := v_alteracoes || jsonb_build_object('modelo', jsonb_build_array(OLD.modelo, NEW.modelo));
        END IF;
        IF (OLD.xml_original IS DISTINCT FROM NEW.xml_original) THEN
            v_alteracoes := v_alteracoes || jsonb_build_object('xml_original', jsonb_build_array('(anterior)', '(substituído)'));
        END IF;
        IF ((OLD.xml_assinado IS NULL) IS DISTINCT FROM (NEW.xml_assinado IS NULL)) THEN
            v_alteracoes := v_alteracoes || jsonb_build_object('xml_assinado', jsonb_build_array(OLD.xml_assinado IS NOT NULL, NEW.xml_assinado IS NOT NULL));
        END IF;

        IF (v_alteracoes <> '{}'::JSONB) THEN
            INSERT INTO timeline_eventos_fiscais (
                documento_id, origem_evento, etapa, status_anterior, status_atual,
                alteracoes_campos, detalhe_erro, sucesso
            ) VALUES (
                NEW.id, 'BANCO_TRIGGER', 'ATUALIZACAO_DOCUMENTO', OLD.status_validacao, NEW.status_validacao,
                v_alteracoes, NEW.mensagem_erro,
                (NEW.status_validacao <> 'ERRO')
            );
        END IF;
        RETURN NEW;

    ELSIF (TG_OP = 'DELETE') THEN
        INSERT INTO timeline_eventos_fiscais (
            documento_id, origem_evento, etapa, status_anterior, status_atual,
            alteracoes_campos, detalhe_erro, sucesso
        ) VALUES (
            OLD.id, 'BANCO_TRIGGER', 'EXCLUSAO_DOCUMENTO', OLD.status_validacao, NULL,
            to_jsonb(OLD) - 'xml_original' - 'xml_assinado',
            OLD.mensagem_erro,
            TRUE
        );
        RETURN OLD;
    END IF;

    RETURN NULL;

EXCEPTION WHEN OTHERS THEN
    -- A auditoria nunca deve impedir a operação principal.
    RAISE WARNING 'Erro ao auditar notas_fiscais [SQLSTATE %]: %', SQLSTATE, SQLERRM;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_auditar_notas_fiscais
AFTER INSERT OR UPDATE OR DELETE ON notas_fiscais
FOR EACH ROW EXECUTE FUNCTION t_auditar_notas_fiscais();
