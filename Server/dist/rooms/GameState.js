"use strict";
var __decorate = (this && this.__decorate) || function (decorators, target, key, desc) {
    var c = arguments.length, r = c < 3 ? target : desc === null ? desc = Object.getOwnPropertyDescriptor(target, key) : desc, d;
    if (typeof Reflect === "object" && typeof Reflect.decorate === "function") r = Reflect.decorate(decorators, target, key, desc);
    else for (var i = decorators.length - 1; i >= 0; i--) if (d = decorators[i]) r = (c < 3 ? d(r) : c > 3 ? d(target, key, r) : d(target, key)) || r;
    return c > 3 && r && Object.defineProperty(target, key, r), r;
};
var __metadata = (this && this.__metadata) || function (k, v) {
    if (typeof Reflect === "object" && typeof Reflect.metadata === "function") return Reflect.metadata(k, v);
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.GameState = exports.PlayerInState = exports.PokemonInState = void 0;
const schema_1 = require("@colyseus/schema");
class PokemonInState extends schema_1.Schema {
    id = "";
    species_id = 0;
    level = 1;
    hp = 0;
    maxHp = 0;
}
exports.PokemonInState = PokemonInState;
__decorate([
    (0, schema_1.type)("string"),
    __metadata("design:type", String)
], PokemonInState.prototype, "id", void 0);
__decorate([
    (0, schema_1.type)("number"),
    __metadata("design:type", Number)
], PokemonInState.prototype, "species_id", void 0);
__decorate([
    (0, schema_1.type)("number"),
    __metadata("design:type", Number)
], PokemonInState.prototype, "level", void 0);
__decorate([
    (0, schema_1.type)("number"),
    __metadata("design:type", Number)
], PokemonInState.prototype, "hp", void 0);
__decorate([
    (0, schema_1.type)("number"),
    __metadata("design:type", Number)
], PokemonInState.prototype, "maxHp", void 0);
class PlayerInState extends schema_1.Schema {
    id = "";
    name = "Trainer";
    x = 0;
    y = 0;
    z = 0;
    party = new schema_1.MapSchema();
}
exports.PlayerInState = PlayerInState;
__decorate([
    (0, schema_1.type)("string"),
    __metadata("design:type", String)
], PlayerInState.prototype, "id", void 0);
__decorate([
    (0, schema_1.type)("string"),
    __metadata("design:type", String)
], PlayerInState.prototype, "name", void 0);
__decorate([
    (0, schema_1.type)("number"),
    __metadata("design:type", Number)
], PlayerInState.prototype, "x", void 0);
__decorate([
    (0, schema_1.type)("number"),
    __metadata("design:type", Number)
], PlayerInState.prototype, "y", void 0);
__decorate([
    (0, schema_1.type)("number"),
    __metadata("design:type", Number)
], PlayerInState.prototype, "z", void 0);
__decorate([
    (0, schema_1.type)({ map: PokemonInState }),
    __metadata("design:type", Object)
], PlayerInState.prototype, "party", void 0);
class GameState extends schema_1.Schema {
    players = new schema_1.MapSchema();
}
exports.GameState = GameState;
__decorate([
    (0, schema_1.type)({ map: PlayerInState }),
    __metadata("design:type", Object)
], GameState.prototype, "players", void 0);
