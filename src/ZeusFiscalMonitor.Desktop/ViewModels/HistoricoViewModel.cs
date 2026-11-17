using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using ZeusFiscalMonitor.Domain.DTOs;
using ZeusFiscalMonitor.Domain.Exceptions;
using ZeusFiscalMonitor.Domain.Interfaces;
using ZeusFiscalMonitor.Domain.ValueObjects;

namespace ZeusFiscalMonitor.Desktop.ViewModels;

/// <summary>Aba 3 — timeline de auditoria (eventos da aplicação + eventos da trigger do banco).</summary>
public partial class HistoricoViewModel : ObservableObject
{
    private const int Limite = 200;
    private readonly IFiscalRepository _repositorio;

    public HistoricoViewModel(IFiscalRepository repositorio, IMessenger messenger)
    {
        _repositorio = repositorio;
        messenger.Register<HistoricoViewModel, DocumentoProcessadoMensagem>(this,
            static (vm, _) => vm.AtualizarCommand.Execute(null));
    }

    public ObservableCollection<HistoricoOperacaoDto> Eventos { get; } = [];

    [ObservableProperty] private string _filtroChave = string.Empty;
    [ObservableProperty] private bool _carregando;
    [ObservableProperty] private string? _mensagem;
    [ObservableProperty] private HistoricoOperacaoDto? _eventoSelecionado;

    [RelayCommand]
    private async Task AtualizarAsync(CancellationToken ct)
    {
        Carregando = true;
        Mensagem = null;

        try
        {
            var filtro = string.IsNullOrWhiteSpace(FiltroChave) ? null : new ChaveAcesso(FiltroChave);
            var itens = await _repositorio.ListarHistoricoAsync(filtro, Limite, ct);

            Eventos.Clear();
            foreach (var item in itens)
                Eventos.Add(item);

            Mensagem = itens.Count == 0 ? "Nenhum evento encontrado." : $"{itens.Count} evento(s) — mais recentes primeiro (limite {Limite}).";
        }
        catch (DomainException ex)
        {
            Mensagem = $"Filtro inválido: {ex.Message}";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Mensagem = $"Não foi possível consultar o banco: {ex.GetBaseException().Message}";
        }
        finally
        {
            Carregando = false;
        }
    }
}
