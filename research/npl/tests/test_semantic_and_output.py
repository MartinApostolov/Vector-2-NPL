from __future__ import annotations

import unittest
from dataclasses import FrozenInstanceError

from vector_npl_research.model_output import parse_model_output
from vector_npl_research.semantic import (
    BooleanLiteral,
    Comparison,
    ComparisonKind,
    CurrentItem,
    Identifier,
    NumberLiteral,
    SemanticValidationError,
    TextLiteral,
    parse_semantic_json,
)


VALID_COMPARISON = (
    '{"type":"comparison","kind":"GTE",'
    '"left":{"type":"identifier","name":"score"},'
    '"right":{"type":"numberLiteral","value":10}}'
)


class SemanticParserTests(unittest.TestCase):
    def test_all_p01_nodes_parse_to_immutable_values(self) -> None:
        comparison = parse_semantic_json(VALID_COMPARISON)
        self.assertEqual(
            Comparison(ComparisonKind.GTE, Identifier("score"), NumberLiteral(10)),
            comparison,
        )
        self.assertEqual(TextLiteral(""), parse_semantic_json('{"type":"textLiteral","value":""}'))
        self.assertEqual(
            BooleanLiteral(True),
            parse_semantic_json('{"type":"booleanLiteral","value":true}'),
        )
        self.assertEqual(CurrentItem(), parse_semantic_json('{"type":"currentItem"}'))
        with self.assertRaises(FrozenInstanceError):
            comparison.kind = ComparisonKind.GT  # type: ignore[misc]

    def test_duplicate_members_are_rejected(self) -> None:
        self.assert_semantic_invalid(
            '{"type":"identifier","name":"score","name":"other"}',
            "JSON_MEMBER_DUPLICATE",
        )

    def test_unknown_members_and_node_types_are_rejected(self) -> None:
        self.assert_semantic_invalid('{"type":"currentItem","value":1}', "SEMANTIC_MEMBER_UNKNOWN")
        self.assert_semantic_invalid('{"type":"futureNode"}', "SEMANTIC_TYPE_UNKNOWN")

    def test_comparison_kind_is_exact_case_sensitive_string(self) -> None:
        self.assert_semantic_invalid(VALID_COMPARISON.replace('"GTE"', '"gte"'), "COMPARISON_KIND_INVALID")
        self.assert_semantic_invalid(VALID_COMPARISON.replace('"GTE"', "1"), "COMPARISON_KIND_INVALID")

    def test_identifier_must_be_nonblank(self) -> None:
        self.assert_semantic_invalid('{"type":"identifier","name":"  "}', "IDENTIFIER_NAME_REQUIRED")

    def test_text_may_be_empty_but_not_null(self) -> None:
        self.assertEqual(TextLiteral(""), parse_semantic_json('{"type":"textLiteral","value":""}'))
        self.assert_semantic_invalid('{"type":"textLiteral","value":null}', "TEXT_VALUE_REQUIRED")

    def test_numbers_must_be_json_numbers_finite_and_not_boolean(self) -> None:
        self.assert_semantic_invalid('{"type":"numberLiteral","value":"10"}', "NUMBER_VALUE_INVALID")
        self.assert_semantic_invalid('{"type":"numberLiteral","value":true}', "NUMBER_VALUE_INVALID")
        self.assert_semantic_invalid('{"type":"numberLiteral","value":NaN}', "JSON_NUMBER_NOT_FINITE")
        self.assert_semantic_invalid('{"type":"numberLiteral","value":Infinity}', "JSON_NUMBER_NOT_FINITE")

    def test_current_item_has_no_payload(self) -> None:
        self.assert_semantic_invalid(
            '{"type":"currentItem","value":null}',
            "SEMANTIC_MEMBER_UNKNOWN",
        )

    def test_comparison_operands_must_be_valid_semantic_nodes(self) -> None:
        self.assert_semantic_invalid(
            VALID_COMPARISON.replace(
                '{"type":"identifier","name":"score"}',
                '{"type":"identifier","name":" "}',
            ),
            "IDENTIFIER_NAME_REQUIRED",
        )

    def assert_semantic_invalid(self, text: str, code: str) -> None:
        with self.assertRaises(SemanticValidationError) as context:
            parse_semantic_json(text)
        self.assertEqual(code, context.exception.code)


class ModelOutputParserTests(unittest.TestCase):
    def test_valid_semantic_envelope(self) -> None:
        parsed = parse_model_output(
            f'{{"outcome":"semantic","semantic":{VALID_COMPARISON}}}'
        )
        self.assertTrue(parsed.valid)
        self.assertEqual("semantic", parsed.outcome)
        self.assertIsInstance(parsed.semantic, Comparison)

    def test_valid_reject_envelope(self) -> None:
        parsed = parse_model_output('{"outcome":"reject"}')
        self.assertTrue(parsed.valid)
        self.assertEqual("reject", parsed.outcome)
        self.assertIsNone(parsed.semantic)

    def test_bare_semantic_ir_is_invalid_not_reject(self) -> None:
        parsed = parse_model_output(VALID_COMPARISON)
        self.assertEqual("invalid", parsed.outcome)
        self.assertEqual("OUTPUT_OUTCOME_REQUIRED", parsed.error_code)

    def test_free_form_and_malformed_output_are_invalid(self) -> None:
        for raw in ("GTE", "reject because ambiguous", '{"outcome":'):
            with self.subTest(raw=raw):
                self.assertEqual("invalid", parse_model_output(raw).outcome)

    def test_duplicate_envelope_member_is_invalid(self) -> None:
        parsed = parse_model_output('{"outcome":"reject","outcome":"semantic"}')
        self.assertEqual("invalid", parsed.outcome)
        self.assertEqual("JSON_MEMBER_DUPLICATE", parsed.error_code)

    def test_unknown_envelope_member_is_invalid(self) -> None:
        parsed = parse_model_output('{"outcome":"reject","reason":"ambiguous"}')
        self.assertEqual("invalid", parsed.outcome)
        self.assertEqual("OUTPUT_MEMBER_UNKNOWN", parsed.error_code)

    def test_missing_or_badly_cased_outcome_is_invalid(self) -> None:
        self.assertEqual("invalid", parse_model_output("{}").outcome)
        parsed = parse_model_output('{"outcome":"Reject"}')
        self.assertEqual("invalid", parsed.outcome)
        self.assertEqual("OUTPUT_OUTCOME_UNSUPPORTED", parsed.error_code)

    def test_semantic_payload_is_required(self) -> None:
        parsed = parse_model_output('{"outcome":"semantic"}')
        self.assertEqual("invalid", parsed.outcome)
        self.assertEqual("OUTPUT_MEMBER_REQUIRED", parsed.error_code)

    def test_reject_must_not_contain_semantic_payload(self) -> None:
        parsed = parse_model_output(
            f'{{"outcome":"reject","semantic":{VALID_COMPARISON}}}'
        )
        self.assertEqual("invalid", parsed.outcome)
        self.assertEqual("OUTPUT_MEMBER_UNKNOWN", parsed.error_code)

    def test_invalid_semantic_payload_is_invalid(self) -> None:
        parsed = parse_model_output(
            '{"outcome":"semantic","semantic":{"type":"identifier","name":" "}}'
        )
        self.assertEqual("invalid", parsed.outcome)
        self.assertEqual("IDENTIFIER_NAME_REQUIRED", parsed.error_code)

    def test_noncomparison_semantic_root_is_invalid(self) -> None:
        parsed = parse_model_output(
            '{"outcome":"semantic","semantic":{"type":"currentItem"}}'
        )
        self.assertEqual("invalid", parsed.outcome)
        self.assertEqual("OUTPUT_COMPARISON_REQUIRED", parsed.error_code)

    def test_nonfinite_and_bool_number_payloads_are_invalid(self) -> None:
        for value in ("NaN", "true"):
            with self.subTest(value=value):
                raw = (
                    '{"outcome":"semantic","semantic":{"type":"comparison","kind":"EQ",'
                    '"left":{"type":"currentItem"},'
                    '"right":{"type":"numberLiteral","value":' + value + '}}}'
                )
                self.assertEqual("invalid", parse_model_output(raw).outcome)


if __name__ == "__main__":
    unittest.main()
