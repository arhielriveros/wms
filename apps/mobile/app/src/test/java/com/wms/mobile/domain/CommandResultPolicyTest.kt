package com.wms.mobile.domain

import org.junit.Assert.assertEquals
import org.junit.Test

class CommandResultPolicyTest {
    @Test
    fun `accepted y already processed se reconocen como confirmados idempotentes`() {
        assertEquals(CommandDisposition.ACKNOWLEDGED, CommandResultPolicy.disposition(CommandResultStatus.Accepted))
        assertEquals(CommandDisposition.ACKNOWLEDGED, CommandResultPolicy.disposition(CommandResultStatus.AlreadyProcessed))
    }

    @Test
    fun `conflict y requires review nunca se confirman automáticamente`() {
        assertEquals(CommandDisposition.CONFLICT, CommandResultPolicy.disposition(CommandResultStatus.Conflict))
        assertEquals(CommandDisposition.CONFLICT, CommandResultPolicy.disposition(CommandResultStatus.RequiresReview))
    }

    @Test
    fun `todos los resultados wire tienen disposición explícita`() {
        val expected = mapOf(
            "Accepted" to CommandDisposition.ACKNOWLEDGED,
            "Rejected" to CommandDisposition.REJECTED,
            "Conflict" to CommandDisposition.CONFLICT,
            "AlreadyProcessed" to CommandDisposition.ACKNOWLEDGED,
            "RequiresReview" to CommandDisposition.CONFLICT,
            "Expired" to CommandDisposition.EXPIRED,
            "Unauthorized" to CommandDisposition.AUTH_REQUIRED,
            "future-value" to CommandDisposition.RETRY,
        )

        expected.forEach { (wire, disposition) ->
            assertEquals(disposition, CommandResultPolicy.disposition(CommandResultStatus.fromWire(wire)))
        }
    }
}
