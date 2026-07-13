package com.wms.mobile.domain

import org.junit.Assert.assertEquals
import org.junit.Test

class CommandBatchPlannerTest {
    @Test
    fun `ordena comandos por tarea y secuencia local`() {
        val input = listOf(
            PendingCommand("b-2", "task-b", 2),
            PendingCommand("a-3", "task-a", 3),
            PendingCommand("a-1", "task-a", 1),
            PendingCommand("b-1", "task-b", 1),
        )

        val planned = CommandBatchPlanner.plan(input)

        assertEquals(listOf("a-1", "a-3", "b-1", "b-2"), planned.map { it.commandId })
    }

    @Test
    fun `un commandId duplicado produce un solo envío`() {
        val input = listOf(
            PendingCommand("same-id", "task-a", 1),
            PendingCommand("same-id", "task-a", 1),
        )

        assertEquals(1, CommandBatchPlanner.plan(input).size)
    }

    @Test
    fun `el batch nunca supera cien comandos`() {
        val input = (1..130).map { PendingCommand("id-$it", "task-a", it.toLong()) }

        assertEquals(100, CommandBatchPlanner.plan(input, limit = 500).size)
    }
}
