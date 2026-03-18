"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.PokemonInstanceModel = void 0;
const mongoose_1 = __importStar(require("mongoose"));
const PokemonInstanceSchema = new mongoose_1.Schema({
    owner_id: { type: mongoose_1.Schema.Types.ObjectId, ref: 'Player', required: true, index: true },
    species_id: { type: Number, required: true },
    nickname: { type: String, default: '' },
    level: { type: Number, default: 1 },
    exp: { type: Number, default: 0 },
    nature: { type: String, required: true },
    current_hp: { type: Number, required: true },
    max_hp: { type: Number, required: true },
    status_condition: { type: String, default: 'NONE' },
    is_in_party: { type: Boolean, default: false },
    party_slot: { type: Number, min: 1, max: 6 } // Only required/validated if is_in_party is true
});
// Adding a compound index to quickly find a player's party
PokemonInstanceSchema.index({ owner_id: 1, is_in_party: 1 });
exports.PokemonInstanceModel = mongoose_1.default.model('PokemonInstance', PokemonInstanceSchema);
//# sourceMappingURL=PokemonInstance.js.map