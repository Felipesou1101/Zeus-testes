Create Or REPLACE FUNCTION t_auditar_notas_fiscais()
RETURNS TRIGGER AS $$
Declare
    v_alteracoes JSONB := '{}'::JSONB;
    v_chave_acesso TEXT;
BEGIN
    IF (TG_OP = 'INSERT') THEN
        INSERT INTO timeline_eventos_fiscais (
            documento_id, origem_evento, etapa, status_anterior, status_atual,
            alteracoes_campos, detalhe_erro, sucesso
        ) VALUES (
            NEW.id, 'BANCO_TRIGGER', 'CRIACAO_DOCUMENTO', NULL, NEW.status_validacao,
            to_jsonb(NEW) - 'xml_original' - 'xml_assinado', -- Omite XML gigante para manter o log leve
            NEW.mensagem_erro,
            (NEW.status_validacao <> 'ERRO')
        );
        RETURN NEW;

    ELSIF (TG_OP = 'UPDATE') THEN
        v_chave_acesso := COALESCE(NEW.chave_acesso, OLD.chave_acesso);
        -- Compara os campos relevantes e registra apenas as alterações
        IF (OLD.status_validacao IS DISTINCT FROM NEW.status_validacao) THEN
            v_alteracoes := v_alteracoes || jsonb_build_object('status_validacao', jsonb_build_array(OLD.status_validacao, NEW.status_validacao));
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
            (OLD.status_validacao <> 'ERRO')
        );
        RETURN OLD;
    END IF;
    RETURN NULL; -- Caso não seja INSERT, UPDATE ou DELETE, não faz nada

    EXCEPTION WHEN OTHERS THEN
        RAISE WARNING 'Erro ao auditar notas fiscais [SQLSTATE %]: %', SQLSTATE, SQLERRM;
        RETURN COALESCE(NEW, OLD);    
END;

$$ LANGUAGE plpgsql;

Create TRIGGER trg_auditar_notas_fiscais
AFTER INSERT OR UPDATE OR DELETE ON notas_fiscais
FOR EACH ROW EXECUTE FUNCTION t_auditar_notas_fiscais();