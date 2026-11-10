using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DocumentManagement.Models;
using DocumentManagement.Services;

namespace DocumentManagement.ViewModels;

/// <summary>
/// ViewModel for AI Chat functionality - uses shared AiChatService
/// </summary>
public partial class AiChatViewModel(
    AIService aiService,
    Func<Document?> getSelectedDocument,
    Func<IEnumerable<Document>> getAllDocuments) : BaseViewModel
{
    private readonly AiChatService _aiChatService = new(aiService);
    private readonly Func<Document?> _getSelectedDocument = getSelectedDocument;
    private readonly Func<IEnumerable<Document>> _getAllDocuments = getAllDocuments;

    [ObservableProperty]
    private bool _isAiChatOpen;

    [ObservableProperty]
    private string _chatInput = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _chatMessages = [];

    [ObservableProperty]
    private bool _isChatLoading;



    [RelayCommand]
    private void ToggleAiChat()
    {
        IsAiChatOpen = !IsAiChatOpen;
    }

    [RelayCommand]
    private async Task SendChatMessage()
    {
        if (string.IsNullOrWhiteSpace(ChatInput) || IsChatLoading) return;

        var userMessage = ChatInput.Trim();
        ChatInput = string.Empty;

        // Add user message
        Application.Current.Dispatcher.Invoke(() =>
        {
            ChatMessages.Add(new ChatMessage { Content = userMessage, IsUser = true });
        });

        IsChatLoading = true;

        try
        {
            // Get history excluding the message we just added
            List<ChatMessage> history = [.. ChatMessages.Where(m => m.Content != userMessage || !m.IsUser)];

            var response = await _aiChatService.SendChatAsync(
                userMessage,
                history,
                _getSelectedDocument(),
                _getAllDocuments());

            Application.Current.Dispatcher.Invoke(() =>
            {
                ChatMessages.Add(new ChatMessage { Content = response, IsUser = false });
            });
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ChatMessages.Add(new ChatMessage { Content = $"Lỗi: {ex.Message}", IsUser = false });
            });
        }
        finally
        {
            IsChatLoading = false;
        }
    }

    [RelayCommand]
    private void ClearChat()
    {
        ChatMessages.Clear();
    }
}
