// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoBot v4.18.1

using Azure.AI.OpenAI;
using Azure;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;


namespace EchoBot1.Bots
{
    public class EchoBot : ActivityHandler
    {
        // Azure AI Search setup
        static string searchEndpoint = ""; // Add your Azure AI Search endpoint here
        static string searchKey = ""; // Add your Azure AI Search admin key here
        static string searchIndexName = ""; // Add your Azure AI Search index name here

        public static ChatCompletionsOptions options = new ChatCompletionsOptions()
        {
            Messages = { new ChatMessage(ChatRole.System, @"You are an AI assistant that helps people find information.") },
            Temperature = (float)0.7,
            MaxTokens = 200,
            AzureExtensionsOptions = new AzureChatExtensionsOptions()
            {
                Extensions =
                {
                    new AzureCognitiveSearchChatExtensionConfiguration()
                    {
                        SearchEndpoint = new Uri(searchEndpoint),
                        IndexName = searchIndexName,
                        SearchKey = new AzureKeyCredential(searchKey!),
                    }
                }
            }
        };

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            string prompt = turnContext.Activity.Text;
            var resultResponse = await CompletionAPIHandler(prompt);
            if (resultResponse != null)
            {
                await turnContext.SendActivityAsync(MessageFactory.Text(resultResponse), cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text("We are having some trouble communicating with our servers." +
                    " Please try again later"), cancellationToken);
            }
        }

        private async Task<string?> CompletionAPIHandler(string prompt)
        {
            string azureOpenAIEndpoint = ""; //Add your azure openai endpoint here
            string azureOpenAIKey = ""; //Add your azure OpenAi key
            string userMessage = prompt; 

            OpenAIClient client = new OpenAIClient(
                new Uri(azureOpenAIEndpoint),
                new AzureKeyCredential(azureOpenAIKey));

            while (true)
            {
                options.Messages.Add(new ChatMessage(ChatRole.User, userMessage));

                Console.WriteLine("Response:");
                Response<StreamingChatCompletions> response =
                await client.GetChatCompletionsStreamingAsync(
                    "",
                    options);


                using StreamingChatCompletions streamingChatCompletions = response.Value;
                string fullresponse = "";
                await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming())
                {
                    await foreach (ChatMessage message in choice.GetMessageStreaming())
                    {
                        fullresponse += message.Content;
                    }
                }
                options.Messages.Add(new ChatMessage(ChatRole.Assistant, fullresponse));
                if (fullresponse != null)
                {
                    return (string)fullresponse;
                }
                else return null;
            }
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and how can I assist you?";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}
