import { Agent, tool } from '@strands-agents/sdk';
import { OpenAIModel } from '@strands-agents/sdk/openai';
import * as z from "zod";
import { loggingFetch } from './loggingFetch.js';

async function main() {

    // Initialize the OpenAI model
    const model = new OpenAIModel({
        modelId: process.env.MODEL_ID,
        apiKey: process.env.OPENAI_API_KEY,
        clientConfig: {
            baseURL: process.env.OPENAI_API_BASE_URL,
            fetch: loggingFetch
        }
    });

    const weatherTool = tool({
        name: 'weather_forecast',
        description: 'Get weather forecast for a city',
        inputSchema: z.object({
            city: z.string().describe('The name of the city'),
            days: z.number().default(3).describe('Number of days for the forecast'),
        }),
        callback: (input) => {
            return `Weather forecast for ${input.city} for the next ${input.days} days is cloudy with a high of 15°C`
        },
    })

    // Create the agent
    const agent = new Agent({
        model: model,
        systemPrompt: "You are a helpful assistant",
        printer: false,
        tools: [weatherTool]
    });

    // Run the agent
    console.log("Asking for weather forecast...\n");
    for await (const event of agent.stream("Give me a weather forecast Amsterdam for the next five days")) {
        if (event.type === 'modelContentBlockDeltaEvent' && event.delta.type === 'textDelta') {
            process.stdout.write(event.delta.text);
        }
    }

    console.log("\nAgent call completed.");
}

main().catch((err) => {
    console.error(err);
    process.exitCode = 1;
});