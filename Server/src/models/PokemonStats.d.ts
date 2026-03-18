import mongoose, { Document, Types } from 'mongoose';
export interface IStatBlock {
    hp: number;
    atk: number;
    def: number;
    spatk: number;
    spdef: number;
    spd: number;
}
export interface IPokemonStats extends Document {
    pokemon_instance_id: Types.ObjectId;
    ivs: IStatBlock;
    evs: IStatBlock;
}
export declare const PokemonStatsModel: mongoose.Model<IPokemonStats, {}, {}, {}, mongoose.Document<unknown, {}, IPokemonStats, {}, mongoose.DefaultSchemaOptions> & IPokemonStats & Required<{
    _id: Types.ObjectId;
}> & {
    __v: number;
} & {
    id: string;
}, any, IPokemonStats>;
//# sourceMappingURL=PokemonStats.d.ts.map