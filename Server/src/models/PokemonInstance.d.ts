import mongoose, { Document, Types } from 'mongoose';
export interface IPokemonInstance extends Document {
    owner_id: Types.ObjectId;
    species_id: number;
    nickname: string;
    level: number;
    exp: number;
    nature: string;
    current_hp: number;
    max_hp: number;
    status_condition: string;
    is_in_party: boolean;
    party_slot: number;
}
export declare const PokemonInstanceModel: mongoose.Model<IPokemonInstance, {}, {}, {}, mongoose.Document<unknown, {}, IPokemonInstance, {}, mongoose.DefaultSchemaOptions> & IPokemonInstance & Required<{
    _id: Types.ObjectId;
}> & {
    __v: number;
} & {
    id: string;
}, any, IPokemonInstance>;
//# sourceMappingURL=PokemonInstance.d.ts.map