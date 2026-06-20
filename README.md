# Simple Role Sync

A Discord bot that observes user's Discord presence data to automatically update their profile on avatar update.

# Run

> Docker should be installed on the host machine (optional)
> The project was tested to be functional using a Docker setup. In a standalone setup, the functionality may differ.

### Steps

- Clone the repository
- Rename the sample environment file to `discord_bot.env` (remove `.sample`)
- Paste your Discord bot token. [Click here to view your Discord applications](https://discord.com/developers/applications)
  - The environment variable should contain no space characters
    ```dotenv
    # Example (invalid token)
    BOT__TOKEN=XXXXXXXXXXXXXXXXXXXXXXXXXX.XXXXXX.XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
    ```
- Run `docker compose up -d`
