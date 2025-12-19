namespace Common.Enums;

public enum CommandResponse : byte
{
    Ok,
    GameNotFound,
    PlayerNotFound,
    NotYourTurn,
    InvalidAction,
    GameFull,
    GameAlreadyStarted,
    CardNotFound,
    NotEnoughCards,
    PlayerNotAlive,
    SessionNotFound,
    Unauthorized
}