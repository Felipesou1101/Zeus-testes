CREATE TABLE notas_fiscais (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chave_acesso VARCHAR(44) UNIQUE,
    modelo VARCHAR(2) NOT NULL DEFAULT '55',  -- '55' para NF-e, '65' para NFC-e
    uf_emitente VARCHAR(2) NOT NULL,
    xml_original TEXT NOT NULL,
    xml_assinado TEXT,
    status VARCHAR(20) NOT NULL DEFAULT 'PENDENTE',  -- 'PENDENTE', 'PROCESSANDO', 'PROCESSADO', 'ERRO'
    mensagem_erro TEXT,
    criado_em TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    atualizado_em TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP   
);

Create INDEX idx_notas_fiscais_chave_acesso 
ON notas_fiscais (criado_em DESC, chave_acesso);

CREATE TABLE timeline_eventos_fiscais (
    evento_id BIGSERIAL PRIMARY KEY,
    documento_id UUID NOT NULL,
    origem_evento VARCHAR(20) NOT NULL, -- 'BANCO_TRIGGER' ou 'APLICACAO_CSHARP'
    etapa VARCHAR(40) NOT NULL,         -- 'CRIACAO', 'VALIDACAO_XSD', 'ASSINATURA', 'ENVIO_SEFAZ', 'ERRO'
    status_anterior VARCHAR(30),
    status_atual VARCHAR(30),
    alteracoes_campos JSONB,            -- Apenas o que mudou naquele instante (diff)
    detalhe_erro TEXT,
    sucesso BOOLEAN NOT NULL,
    ocorrido_em TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp() -- Hora exata da execução (não da transação)
);

CREATE INDEX idx_timeline_documento_tempo 
ON timeline_eventos_fiscais (documento_id, ocorrido_em ASC, evento_id ASC);