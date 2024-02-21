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
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;


namespace EchoBot1.Bots
{
    public class EchoBot : ActivityHandler
    {
        // Azure AI Search setup
        static string searchEndpoint = ""; // Add your Azure AI Search endpoint here
        static string searchKey = ""; // Add your Azure AI Search admin key here
        static string searchIndexName = ""; // Add your Azure AI Search index name here

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
            string azureOpenAIEndpoint = "";
            string azureOpenAIKey = "";
            string userMessage = prompt;

            OpenAIClient client = new OpenAIClient(
                new Uri(azureOpenAIEndpoint),
                new AzureKeyCredential(azureOpenAIKey));


            ChatCompletionsOptions options = new ChatCompletionsOptions()
            {
                Messages = { new ChatRequestSystemMessage(@"Your name is Albert. Albert is a Chatbot for ... 
                                Albert has access to all the conversation history and previous messages. Albert can answer using the previous conversation history and messages. Albert speaks fluent English.  -------------------- 
                                Output style: Well written whole sentences. No short answers! --------------------"),
                                new ChatRequestUserMessage(userMessage)},
                Temperature = 1,
                MaxTokens = 1200,
                AzureExtensionsOptions = new AzureChatExtensionsOptions()
                {
                    Extensions =
                     {
                        new AzureCognitiveSearchChatExtensionConfiguration()
                        {
                            SearchEndpoint = new Uri(searchEndpoint),
                            IndexName = searchIndexName,
                            Key  = searchKey,
                        }
                    }
                },
                DeploymentName = "",
            };
            static Dictionary<int, string> ExtractLinks(string jsonString)
            {
                Dictionary<int, string> links = new Dictionary<int, string>();

                try
                {
                    JObject jsonObject = JObject.Parse(jsonString);

                    // Check if the JSON contains "citations" array
                    if (jsonObject["citations"] != null)
                    {
                        JArray citationsArray = (JArray)jsonObject["citations"];

                        // Iterate through each citation
                        int linkNumber = 1; // Initialize link number
                        foreach (JObject citation in citationsArray)
                        {
                            // Check if the citation contains "url"
                            if (citation["url"] != null)
                            {
                                string url = citation["url"].ToString();
                                links.Add(linkNumber, url);
                                linkNumber++; // Increment link number for the next link
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error extracting links: " + ex.Message);
                }

                return links;
            }
            while (true)
            {
                Response<ChatCompletions> response = await client.GetChatCompletionsAsync(options);
                ChatResponseMessage responseMessage = response.Value.Choices[0].Message;
                ChatChoice responseChoice = response.Value.Choices[0];

              
                // Add the assistant message with tool calls to the conversation history
                ChatRequestAssistantMessage toolCallHistoryMessage = new(responseChoice.Message);
                options.Messages.Add(toolCallHistoryMessage);

                // Extract links from JSON
                Dictionary<int, string> links = ExtractLinks(response.Value.Choices[0].Message.AzureExtensionsContext.Messages[0].Content);
                HashSet<string> knownValues = new HashSet<string>();
                Dictionary<int, string> uniqueValues = new Dictionary<int, string>();

                foreach (var pair in links)
                {
                    if (knownValues.Add(pair.Value))
                    {
                        uniqueValues.Add(pair.Key, pair.Value);
                    }
                }

                // Construct final answer with numbered links
                string finalAnswer = responseChoice.Message.Content + "\nSources: \n";
                foreach (var kvp in uniqueValues)
                {
                    finalAnswer += $"{kvp.Key}. {kvp.Value}\n";
                }


                if (responseChoice.Message.Content != null)
                {
                    string result = Regex.Replace(finalAnswer, @"(\[[\w\-?!\""'$§&]+\])", "");

                    return result;
                }
                else return null;


            }
        
    }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome!";
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
