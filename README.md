# microbroker

[![Build & Release](https://github.com/tonycknight/microbroker/actions/workflows/build.yml/badge.svg)](https://github.com/tonycknight/microbroker/actions/workflows/build.yml)

![NuGet Version](https://img.shields.io/nuget/:variant/Microbroker.Client)

A _very simple_ message brokerage service.

Messages are handled in FIFO order, with at-most-once delivery semantics. Transactional messaging is not supported at this time - it's a _very simple_ broker after all!

## How to install

A docker image is available [from the Github Container Registry](https://github.com/users/tonycknight/packages/container/package/microbroker).

```
docker pull ghcr.io/tonycknight/microbroker:<latest tag>
```

You'll also need a MongoDB database installed, available and protected. _Please note that the database is not created nor maintained. See the [MongoDB documentation on how to install and create databases](https://www.mongodb.com/docs/manual/tutorial/getting-started/)_

## How to run

Start the container:

```
docker run -it --rm --publish 8080:8080 ghcr.io/tonycknight/microbroker:<tag> --mongoDbName "<database name>" --mongoConnection "<connection string>" 
```

The parameters you'll need to pass are:

| | |
| - | - |
| `mongoDbName` | The name of the database within the Mongo installation. |
| `mongoConnection` | The connection string to the Mongo DB. | 


## The endpoints

### API endpoints

| | | 
| - | - |
| `GET /api/heartbeat/` | Check the API is alive. |
| `GET /api/queues/` | List active queues. |
| `GET /api/queues/<queue name>/` | Get details on the queue at `queue name`.  | 
| `DELETE /api/queues/<queue name>/` | Delete the queue at `queue name`.  | 
| `GET /api/queues/<queue name>/message/` | Get the next message from the queue at `queue name`. If a message does not await, a `404` will be returned. | 
| `POST /api/queues/<queue name>/message/` | Post a message to the queue at `queue name`. |

#### Posting a message onto a queue

To push a message onto a queue, simply send a `POST` request to `/api/queues/<queue name>/message/`.

The message is in the request's body:

```json
{
    "messageType": "any arbitrary message type of your choice",
    "content": "arbitrary message content"    
}
```


#### Pulling a message from a queue

Simply send a `GET` request to `/api/queues/<queue name>/message/`.

A `200` response will contain the message at the head of the queue.
A `404` response will indicate the queue is empty at that time.

